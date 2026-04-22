using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Drawing;
using System.Windows.Forms;
using LiveWallpaper.Engine;

namespace LiveWallpaper;

public partial class App : System.Windows.Application
{
    private static StreamWriter? _logFile;

    private static void InitLog()
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "livewallpaper.log");
            _logFile = new StreamWriter(logPath, append: false) { AutoFlush = true };
        }
        catch { }
        Log($"[App] LiveWallpaper v3.0 started -- {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Log($"[App] Base: {AppContext.BaseDirectory}");
    }

    public static void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
        _logFile?.WriteLine(line);
    }

    private NotifyIcon?         _tray;
    private MainWindow?         _win;
    private AutoPauseMonitor?   _autoPause;
    private System.Threading.Timer? _trimTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        InitLog();
        base.OnStartup(e);

        // Priority: below normal so wallpaper never competes with foreground apps
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; }
        catch { }

        // FIX: Batch mode was the primary RAM culprit.
        // Batch = GC collects as rarely as possible → heap grows to 300-400 MB.
        // Interactive = GC collects more frequently → heap stays small.
        System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.Interactive;

        DispatcherUnhandledException += (_, ex) =>
        {
            Log($"[App] ERROR: {ex.Exception}");
            System.Windows.MessageBox.Show(ex.Exception.ToString(), "LiveWallpaper Error");
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            Log($"[App] FATAL: {ex.ExceptionObject}");

        _win = new MainWindow();
        MainWindow = _win;
        _win.Show();
        BuildTray();

        // Periodic memory trim: every 60 s while window is hidden (tray-only mode)
        _trimTimer = new System.Threading.Timer(_ => MaybeTrimMemory(), null,
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

        Log("[App] Startup complete");
    }

    // ── Auto-pause lifecycle ─────────────────────────────────────────────────

    public void StartAutoPause(WallpaperEngine wall)
    {
        _autoPause?.Dispose();
        _autoPause = new AutoPauseMonitor(wall);
        Log("[App] AutoPause started");
    }

    public void StopAutoPause()
    {
        _autoPause?.Dispose();
        _autoPause = null;
        Log("[App] AutoPause stopped");
    }

    // ── Memory trim ──────────────────────────────────────────────────────────

    private void MaybeTrimMemory()
    {
        // Only trim while the main window is hidden (user is not interacting)
        bool windowHidden = _win == null || !_win.IsVisible;
        if (!windowHidden) return;

        TrimMemory();
    }

    public static void TrimMemory()
    {
        // Collect all generations, non-blocking
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: false);
        GC.WaitForPendingFinalizers();

        // Ask Windows to trim the working set (pages go to page file, not freed)
        // This reduces Task Manager "Memory" column while the app is idle
        try
        {
            Win32.SetProcessWorkingSetSize(
                Process.GetCurrentProcess().Handle,
                new IntPtr(-1), new IntPtr(-1));
        }
        catch { }
    }

    // ── Tray ─────────────────────────────────────────────────────────────────

    private void BuildTray()
    {
        Icon icon;
        var icoPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        try { icon = new Icon(icoPath); }
        catch { icon = SystemIcons.Application; }

        _tray = new NotifyIcon
        {
            Text    = "LiveWallpaper",
            Icon    = icon,
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show",     null, (_, _) => ShowMain());
        menu.Items.Add("Stop All", null, (_, _) => (_win as MainWindow)?.StopAll());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit",     null, (_, _) => Quit());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick     += (_, _) => ShowMain();
        Log("[App] Tray icon ready");
    }

    private void ShowMain()
    {
        _win?.Show();
        if (_win != null) { _win.WindowState = WindowState.Normal; _win.Activate(); }
    }

    private void Quit()
    {
        (_win as MainWindow)?.StopAll();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("[App] Exit.");
        _trimTimer?.Dispose();
        _tray?.Dispose();
        _autoPause?.Dispose();
        _logFile?.Dispose();
        base.OnExit(e);
    }
}
