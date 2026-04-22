using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace LiveWallpaper.Engine;

/// <summary>
/// Polls the foreground window every 500 ms.
/// Pauses the wallpaper when a fullscreen or maximized app is detected,
/// resumes when the user returns to the normal desktop.
/// </summary>
public sealed class AutoPauseMonitor : IDisposable
{
    private readonly WallpaperEngine _engine;
    private readonly DispatcherTimer _timer;
    private bool _paused;
    private bool _disposed;

    public AutoPauseMonitor(WallpaperEngine engine)
    {
        _engine = engine;
        _timer  = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_disposed) return;

        bool shouldPause = IsFullscreenOrMaximizedAppActive();

        if (shouldPause && !_paused)
        {
            _engine.Pause();
            _paused = true;
            App.Log("[AutoPause] Paused — fullscreen/maximized app detected");
        }
        else if (!shouldPause && _paused)
        {
            _engine.Resume();
            _paused = false;
            App.Log("[AutoPause] Resumed — desktop active");
        }
    }

    private static bool IsFullscreenOrMaximizedAppActive()
    {
        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        // Never pause for the desktop/shell itself
        var sb = new StringBuilder(64);
        Win32.GetClassName(hwnd, sb, sb.Capacity);
        var cls = sb.ToString();
        if (cls is "Progman" or "WorkerW" or "SHELLDLL_DefView" or "Shell_TrayWnd")
            return false;

        // Check for a truly maximized (bordered) window
        if (Win32.IsZoomed(hwnd)) return true;

        // Check for borderless-fullscreen (games, media players)
        if (!Win32.GetWindowRect(hwnd, out var wRect)) return false;
        if (wRect.Width <= 0 || wRect.Height <= 0) return false;

        var monitor = Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONEAREST);
        var mi = new Win32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };
        if (!Win32.GetMonitorInfo(monitor, ref mi)) return false;

        return wRect.left  <= mi.rcMonitor.left  &&
               wRect.top   <= mi.rcMonitor.top   &&
               wRect.right >= mi.rcMonitor.right &&
               wRect.bottom >= mi.rcMonitor.bottom;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();

        // Resume playback on disposal so stopping auto-pause doesn't leave video paused
        if (_paused)
        {
            _engine.Resume();
            _paused = false;
        }
    }
}
