using System.IO;
using System.Windows;
using System.Windows.Controls;
using LiveWallpaper.Engine;

namespace LiveWallpaper.Pages;

public partial class VideoSinglePage : Page
{
    private MainWindow?      Main => Window.GetWindow(this) as MainWindow;
    private WallpaperEngine? Wall => Main?.Wall;
    private Config?          Cfg  => Main?.Cfg;

    private string? _file;
    private bool    _loaded;

    // Populated once on first load; indices map directly to WallpaperEngine.GetMonitors()
    private MonitorInfo[] _monitors = [];

    public VideoSinglePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object s, RoutedEventArgs e)
    {
        // ── Populate monitor list ────────────────────────────────────────────
        _monitors = WallpaperEngine.GetMonitors();
        if (CboMonitor.Items.Count == 0)
        {
            foreach (var m in _monitors)
                CboMonitor.Items.Add(m.Name);
            if (CboMonitor.Items.Count == 0)
                CboMonitor.Items.Add("Monitor 1 (Primary)");
        }
        int savedMonitor = Cfg?.GetInt("single/monitor", 0) ?? 0;
        CboMonitor.SelectedIndex = Math.Clamp(savedMonitor, 0, CboMonitor.Items.Count - 1);

        // ── Populate fit list ────────────────────────────────────────────────
        if (CboFit.Items.Count == 0)
            foreach (var opt in WallpaperEngine.FitOptions)
                CboFit.Items.Add(opt);

        var savedFit = Cfg?.Get("single/fit", WallpaperEngine.FitOptions[1]);
        CboFit.SelectedItem = savedFit ?? WallpaperEngine.FitOptions[1];
        if (CboFit.SelectedItem == null && CboFit.Items.Count > 0)
            CboFit.SelectedIndex = 1;

        // ── Restore sliders ──────────────────────────────────────────────────
        SldVolume.Value   = Cfg?.GetDouble("single/volume",   0)  ?? 0;
        SldSpeed.Value    = Cfg?.GetDouble("single/speed",    0)  ?? 0;
        SldBright.Value   = Cfg?.GetDouble("single/bright",   0)  ?? 0;
        SldContrast.Value = Cfg?.GetDouble("single/contrast", 0)  ?? 0;
        SldPanX.Value     = Cfg?.GetDouble("single/panx",     0)  ?? 0;
        SldPanY.Value     = Cfg?.GetDouble("single/pany",     0)  ?? 0;

        // ── Restore file label ───────────────────────────────────────────────
        _file = Cfg?.Get("single/file");
        if (!string.IsNullOrEmpty(_file) && File.Exists(_file))
        {
            FileLbl.Text       = Path.GetFileName(_file);
            FileLbl.Foreground = (System.Windows.Media.Brush)FindResource("BrTextPri");
        }

        _loaded = true;

        // Auto-start if there's a saved file
        if (!string.IsNullOrEmpty(_file) && File.Exists(_file))
            ApplyWallpaper();
    }

    // ── Browse → auto-apply ───────────────────────────────────────────────────

    private void OnBrowse(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select Video File",
            Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.wmv;*.flv;*.m4v;*.gif|All Files|*.*",
        };
        if (!string.IsNullOrEmpty(_file))
            dlg.InitialDirectory = Path.GetDirectoryName(_file);

        if (dlg.ShowDialog() != true) return;

        _file = dlg.FileName;
        FileLbl.Text       = Path.GetFileName(_file);
        FileLbl.Foreground = (System.Windows.Media.Brush)FindResource("BrTextPri");
        Cfg?.Set("single/file", _file);

        ApplyWallpaper();
    }

    // ── Any setting changed → live update ────────────────────────────────────

    private void OnSettingChanged(object s, EventArgs e)
    {
        if (!_loaded) return;
        SaveSettings();

        if (Wall?.IsPlaying == true && !string.IsNullOrEmpty(_file))
        {
            // Live update — no mpv recreation needed
            Wall.SetSpeed(SldSpeed.IntValue);
            Wall.SetVolume(SldVolume.IntValue);
            Wall.SetBrightness(SldBright.IntValue);
            Wall.SetContrast(SldContrast.IntValue);
            Wall.SetPan(SldPanX.IntValue, SldPanY.IntValue);
            Wall.SetFit(CboFit.SelectedItem?.ToString() ?? WallpaperEngine.FitOptions[1]);
        }
        else if (!string.IsNullOrEmpty(_file) && File.Exists(_file))
        {
            ApplyWallpaper();
        }
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    private void ApplyWallpaper()
    {
        if (string.IsNullOrEmpty(_file)) return;
        SaveSettings();

        int monitorIdx = CboMonitor.SelectedIndex >= 0 ? CboMonitor.SelectedIndex : 0;

        bool ok = Wall?.Play(
            _file,
            speedSlider:  SldSpeed.IntValue,
            brightness:   SldBright.IntValue,
            contrast:     SldContrast.IntValue,
            panX:         SldPanX.IntValue,
            panY:         SldPanY.IntValue,
            fit:          CboFit.SelectedItem?.ToString() ?? WallpaperEngine.FitOptions[1],
            loop:         true,
            volume:       SldVolume.IntValue,
            monitorIndex: monitorIdx) ?? false;

        Main?.SetStatus(ok
            ? $"> {Path.GetFileName(_file)}"
            : "! Could not start wallpaper", ok);
    }

    // ── Stop ─────────────────────────────────────────────────────────────────

    private void OnStop(object s, RoutedEventArgs e)
    {
        Wall?.Stop();
        Main?.SetStatus("Idle  --  no wallpaper active", false);
    }

    // ── Persist (debounced via Config) ────────────────────────────────────────

    private void SaveSettings()
    {
        Cfg?.Set("single/monitor",   CboMonitor.SelectedIndex);
        Cfg?.Set("single/fit",       CboFit.SelectedItem?.ToString() ?? "");
        Cfg?.Set("single/volume",    SldVolume.Value);
        Cfg?.Set("single/speed",     SldSpeed.Value);
        Cfg?.Set("single/bright",    SldBright.Value);
        Cfg?.Set("single/contrast",  SldContrast.Value);
        Cfg?.Set("single/panx",      SldPanX.Value);
        Cfg?.Set("single/pany",      SldPanY.Value);
    }
}
