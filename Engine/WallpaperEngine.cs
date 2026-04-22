using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LiveWallpaper.Engine;

// ── Monitor info ─────────────────────────────────────────────────────────────

public sealed record MonitorInfo(
    string Name,
    System.Drawing.Rectangle Bounds,
    bool IsPrimary);

// ── Engine ───────────────────────────────────────────────────────────────────

public sealed class WallpaperEngine : IDisposable
{
    public static readonly string[] FitOptions =
        ["Scale to Fill", "Stretch to Fit", "Scale to Fit", "Center"];

    private IntPtr  _mpv      = IntPtr.Zero;
    private IntPtr  _hostHwnd = IntPtr.Zero;
    private string? _current;
    private bool    _loopMode = true;
    private bool    _isPaused;
    private System.Drawing.Rectangle _monitorBounds;
    private readonly object _lock = new();

    private List<string> _playlist    = [];
    private int          _playlistIdx = 0;

    public bool    IsPlaying   => _mpv != IntPtr.Zero;
    public string? CurrentFile => _current;
    public bool    IsPaused    => _isPaused;

    private static void Log(string msg) => App.Log(msg);

    // ── Monitor enumeration ──────────────────────────────────────────────────

    public static MonitorInfo[] GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        int index    = 0;

        Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMon, _, ref _, _) =>
        {
            var mi = new Win32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };
            if (Win32.GetMonitorInfo(hMon, ref mi))
            {
                bool primary = (mi.dwFlags & Win32.MONITORINFOF_PRIMARY) != 0;
                var  bounds  = new System.Drawing.Rectangle(
                    mi.rcMonitor.left, mi.rcMonitor.top,
                    mi.rcMonitor.Width, mi.rcMonitor.Height);

                string label = primary
                    ? $"Monitor {index + 1}  (Primary  {bounds.Width}x{bounds.Height})"
                    : $"Monitor {index + 1}  ({bounds.Width}x{bounds.Height})";

                monitors.Add(new MonitorInfo(label, bounds, primary));
                index++;
            }
            return true;
        }, IntPtr.Zero);

        // Sort so primary monitor comes first
        monitors.Sort((a, b) =>
            b.IsPrimary.CompareTo(a.IsPrimary));

        return [.. monitors];
    }

    // ── Progman ──────────────────────────────────────────────────────────────

    private IntPtr GetProgman()
    {
        if (_hostHwnd != IntPtr.Zero && Win32.IsWindow(_hostHwnd))
            return _hostHwnd;

        Log("[Wall] Locating Progman...");
        var pm = Win32.FindWindow("Progman", null);
        if (pm == IntPtr.Zero || !Win32.IsWindow(pm))
        {
            Log("[Wall] ERROR: Progman not found");
            return IntPtr.Zero;
        }
        Log($"[Wall] Progman: {pm}");
        _hostHwnd = pm;
        return pm;
    }

    private void FixZOrder(IntPtr progman)
    {
        var defView = Win32.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView == IntPtr.Zero) return;

        IntPtr mpvWin = IntPtr.Zero;
        Win32.EnumChildWindows(progman, (hwnd, _) =>
        {
            if (hwnd != defView) mpvWin = hwnd;
            return true;
        }, IntPtr.Zero);

        if (mpvWin == IntPtr.Zero) return;

        // Position mpv window on the selected monitor (virtual-screen coords)
        Win32.SetWindowPos(mpvWin, defView,
            _monitorBounds.Left, _monitorBounds.Top,
            _monitorBounds.Width, _monitorBounds.Height,
            Win32.SWP_NOACTIVATE);

        Log($"[Wall] Z-order fixed on monitor {_monitorBounds.Width}x{_monitorBounds.Height} " +
            $"at ({_monitorBounds.Left},{_monitorBounds.Top})");
    }

    // ── MPV instance ─────────────────────────────────────────────────────────

    private bool EnsureMpv(IntPtr host, bool loop)
    {
        if (_mpv != IntPtr.Zero) return true;

        Log("[Core] Creating mpv instance...");
        var ctx = MpvApi.mpv_create();
        if (ctx == IntPtr.Zero)
        {
            Log("[Core] ERROR: mpv_create() failed -- libmpv-2.dll missing?");
            return false;
        }

        MpvApi.SetWid(ctx, host);

        // ── Playback basics ───────────────────────────────────────────────
        MpvApi.mpv_set_option_string(ctx, "loop",                   loop ? "inf" : "no");
        MpvApi.mpv_set_option_string(ctx, "input-default-bindings", "no");
        MpvApi.mpv_set_option_string(ctx, "input-vo-keyboard",      "no");
        MpvApi.mpv_set_option_string(ctx, "osc",                    "no");
        MpvApi.mpv_set_option_string(ctx, "quiet",                  "yes");

        // ── Video output ─────────────────────────────────────────────────
        // hwdec=auto: zero-copy GPU decode — decoded frames stay in VRAM,
        // do NOT copy to system RAM → zero contribution to process RSS.
        MpvApi.mpv_set_option_string(ctx, "hwdec",       "auto");
        MpvApi.mpv_set_option_string(ctx, "vo",          "gpu");
        MpvApi.mpv_set_option_string(ctx, "gpu-context", "auto");

        // ── RAM reduction: demuxer & cache ────────────────────────────────
        // Default demuxer cache is ~150 MB — biggest single RAM hog.
        MpvApi.mpv_set_option_string(ctx, "cache",                  "no");
        MpvApi.mpv_set_option_string(ctx, "demuxer-max-bytes",      "2MiB");
        MpvApi.mpv_set_option_string(ctx, "demuxer-max-back-bytes", "512KiB");

        // ── RAM reduction: decoder threads ───────────────────────────────
        // Fewer threads = fewer internal frame buffer allocations.
        MpvApi.mpv_set_option_string(ctx, "vd-lavc-threads", "2");

        // ── RAM reduction: render pipeline ────────────────────────────────
        // Frame drop over interpolation: never hold extra frames in memory.
        // display-fps-override=30: pretend display is 30 Hz → mpv keeps
        // at most ~2 frames in flight instead of 4-6 at 60+ Hz.
        // This halves the decoder/upload pressure for high-fps content.
        MpvApi.mpv_set_option_string(ctx, "video-sync",           "display-vdrop");
        MpvApi.mpv_set_option_string(ctx, "display-fps-override", "30");
        MpvApi.mpv_set_option_string(ctx, "interpolation",        "no");
        MpvApi.mpv_set_option_string(ctx, "framedrop",            "vo");
        MpvApi.mpv_set_option_string(ctx, "video-latency-hacks",  "yes");

        // ── RAM reduction: skip heavy shaders ────────────────────────────
        // bilinear is the lightest scaler; disabling post-proc skips
        // several GPU buffer allocations for shader uniforms.
        MpvApi.mpv_set_option_string(ctx, "scale",               "bilinear");
        MpvApi.mpv_set_option_string(ctx, "cscale",              "bilinear");
        MpvApi.mpv_set_option_string(ctx, "dscale",              "bilinear");
        MpvApi.mpv_set_option_string(ctx, "correct-downscaling", "no");
        MpvApi.mpv_set_option_string(ctx, "sigmoid-upscaling",   "no");
        MpvApi.mpv_set_option_string(ctx, "hdr-compute-peak",    "no");
        MpvApi.mpv_set_option_string(ctx, "temporal-dither",     "no");
        MpvApi.mpv_set_option_string(ctx, "deband",              "no");
        MpvApi.mpv_set_option_string(ctx, "dither-depth",        "0");

        // ── Volume (muted by default; user can adjust via slider) ─────────
        // We do NOT set audio=no so volume control works.
        // Default = 0 (silent). User raises it intentionally.
        MpvApi.mpv_set_option_string(ctx, "volume",     "0");
        MpvApi.mpv_set_option_string(ctx, "volume-max", "100");

        int err = MpvApi.mpv_initialize(ctx);
        if (err < 0)
        {
            Log($"[Core] ERROR: mpv_initialize() failed: {err}");
            MpvApi.mpv_terminate_destroy(ctx);
            return false;
        }

        _mpv      = ctx;
        _loopMode = loop;
        Log("[Core] mpv initialized OK");
        return true;
    }

    private static void ApplyFit(IntPtr ctx, string fit)
    {
        MpvApi.SetDouble(ctx, "panscan", 0.0);
        MpvApi.mpv_set_property_string(ctx, "video-unscaled", "no");

        switch (fit)
        {
            case "Scale to Fill":
                // Fill screen, ignore aspect ratio (may distort)
                MpvApi.mpv_set_property_string(ctx, "keepaspect", "no");
                break;

            case "Stretch to Fit":
                // Fill screen keeping aspect; crop excess (no black bars)
                MpvApi.mpv_set_property_string(ctx, "keepaspect", "yes");
                MpvApi.SetDouble(ctx, "panscan", 1.0);
                break;

            case "Scale to Fit":
                // Fit entire video; may have black bars
                MpvApi.mpv_set_property_string(ctx, "keepaspect", "yes");
                break;

            case "Center":
                // Original size, centered, no scaling
                MpvApi.mpv_set_property_string(ctx, "keepaspect",     "yes");
                MpvApi.mpv_set_property_string(ctx, "video-unscaled", "yes");
                break;
        }
    }

    // Speed slider: -100..0..+100  →  0.25x..1x..4x (exponential)
    private static double SliderToSpeed(double v) => Math.Pow(2.0, v / 50.0);

    // ── Playback ─────────────────────────────────────────────────────────────

    public bool Play(string path, double speedSlider = 0, int brightness = 0, int contrast = 0,
                     int panX = 0, int panY = 0,
                     string fit    = "Stretch to Fit", bool loop = true,
                     int volume    = 0,
                     int monitorIndex = 0)
    {
        lock (_lock)
        {
            Log($"[Wall] Loading: \"{System.IO.Path.GetFileName(path)}\"");

            // Resolve monitor bounds
            var monitors = GetMonitors();
            _monitorBounds = monitors.Length > 0
                ? monitors[Math.Clamp(monitorIndex, 0, monitors.Length - 1)].Bounds
                : new System.Drawing.Rectangle(
                    0, 0,
                    Win32.GetSystemMetrics(Win32.SM_CXSCREEN),
                    Win32.GetSystemMetrics(Win32.SM_CYSCREEN));

            var host = GetProgman();
            if (host == IntPtr.Zero) return false;

            if (_mpv != IntPtr.Zero && _loopMode != loop)
                DestroyMpv();

            if (!EnsureMpv(host, loop)) return false;

            int err = MpvApi.Command(_mpv, "loadfile", path, "replace");
            if (err < 0)
            {
                Log($"[Wall] ERROR: loadfile failed ({err})");
                return false;
            }

            ApplyFit(_mpv, fit);
            MpvApi.SetDouble(_mpv, "speed",       SliderToSpeed(speedSlider));
            MpvApi.SetDouble(_mpv, "brightness",  brightness);
            MpvApi.SetDouble(_mpv, "contrast",    contrast);
            MpvApi.SetDouble(_mpv, "video-pan-x", panX / 100.0);
            MpvApi.SetDouble(_mpv, "video-pan-y", panY / 100.0);
            MpvApi.SetInt64 (_mpv, "volume",      volume);

            _current  = path;
            _isPaused = false;
            Log("[Wall] Playback started");

            // Re-apply z-order and window position after mpv has settled
            Task.Delay(350).ContinueWith(_ => FixZOrder(host));
            Win32.RedrawWindow(host, IntPtr.Zero, IntPtr.Zero,
                               Win32.RDW_INVALIDATE | Win32.RDW_ALLCHILDREN | Win32.RDW_UPDATENOW);
            return true;
        }
    }

    // ── Playlist ─────────────────────────────────────────────────────────────

    public bool PlayPlaylist(IList<string> files, int index = 0, bool shuffle = false,
                              double speedSlider = 0, int brightness = 0, int contrast = 0,
                              int panX = 0, int panY = 0, string fit = "Stretch to Fit",
                              int volume = 0, int monitorIndex = 0)
    {
        if (files.Count == 0) return false;
        _playlist    = [.. files];
        _playlistIdx = Math.Clamp(index, 0, _playlist.Count - 1);
        if (shuffle)
        {
            Random.Shared.Shuffle(
                System.Runtime.InteropServices.CollectionsMarshal.AsSpan([.. _playlist]));
            _playlistIdx = 0;
        }
        return Play(_playlist[_playlistIdx], speedSlider, brightness, contrast,
                    panX, panY, fit, loop: false, volume, monitorIndex);
    }

    public bool PlaylistNext(double speedSlider = 0, int brightness = 0, int contrast = 0,
                              int panX = 0, int panY = 0, string fit = "Stretch to Fit",
                              int volume = 0, int monitorIndex = 0)
    {
        if (_playlist.Count == 0) return false;
        _playlistIdx = (_playlistIdx + 1) % _playlist.Count;
        return Play(_playlist[_playlistIdx], speedSlider, brightness, contrast,
                    panX, panY, fit, false, volume, monitorIndex);
    }

    public bool PlaylistPrev(double speedSlider = 0, int brightness = 0, int contrast = 0,
                              int panX = 0, int panY = 0, string fit = "Stretch to Fit",
                              int volume = 0, int monitorIndex = 0)
    {
        if (_playlist.Count == 0) return false;
        _playlistIdx = (_playlistIdx - 1 + _playlist.Count) % _playlist.Count;
        return Play(_playlist[_playlistIdx], speedSlider, brightness, contrast,
                    panX, panY, fit, false, volume, monitorIndex);
    }

    public string? PlaylistCurrentName =>
        _playlist.Count > 0 ? System.IO.Path.GetFileName(_playlist[_playlistIdx]) : null;

    // ── Live setters ─────────────────────────────────────────────────────────

    public void SetSpeed(double sliderValue)
    {
        lock (_lock)
            if (_mpv != IntPtr.Zero) MpvApi.SetDouble(_mpv, "speed", SliderToSpeed(sliderValue));
    }

    public void SetVolume(int vol)
    {
        lock (_lock)
            if (_mpv != IntPtr.Zero) MpvApi.SetInt64(_mpv, "volume", Math.Clamp(vol, 0, 100));
    }

    public void SetBrightness(int v)
    {
        lock (_lock)
            if (_mpv != IntPtr.Zero) MpvApi.SetDouble(_mpv, "brightness", v);
    }

    public void SetContrast(int v)
    {
        lock (_lock)
            if (_mpv != IntPtr.Zero) MpvApi.SetDouble(_mpv, "contrast", v);
    }

    public void SetPan(int panX, int panY)
    {
        lock (_lock)
            if (_mpv != IntPtr.Zero)
            {
                MpvApi.SetDouble(_mpv, "video-pan-x", panX / 100.0);
                MpvApi.SetDouble(_mpv, "video-pan-y", panY / 100.0);
            }
    }

    public void SetFit(string fit)
    {
        lock (_lock)
            if (_mpv != IntPtr.Zero) ApplyFit(_mpv, fit);
    }

    // ── Pause / Resume ───────────────────────────────────────────────────────

    public void Pause()
    {
        lock (_lock)
        {
            if (_mpv == IntPtr.Zero || _isPaused) return;
            MpvApi.Command(_mpv, "set", "pause", "yes");
            _isPaused = true;
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_mpv == IntPtr.Zero || !_isPaused) return;
            MpvApi.Command(_mpv, "set", "pause", "no");
            _isPaused = false;
        }
    }

    // ── Stop ─────────────────────────────────────────────────────────────────

    private void DestroyMpv()
    {
        if (_mpv == IntPtr.Zero) return;
        Log("[Core] Stopping mpv...");
        MpvApi.Command(_mpv, "quit");
        MpvApi.mpv_terminate_destroy(_mpv);
        _mpv      = IntPtr.Zero;
        _current  = null;
        _isPaused = false;
    }

    public void Stop()
    {
        lock (_lock)
        {
            DestroyMpv();
            _playlist    = [];
            _playlistIdx = 0;
            _hostHwnd    = IntPtr.Zero;
        }
    }

    public void Dispose() { Stop(); GC.SuppressFinalize(this); }
}
