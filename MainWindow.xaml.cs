using System.Windows;
using System.Windows.Navigation;
using LiveWallpaper.Engine;

namespace LiveWallpaper;

public partial class MainWindow : Window
{
    public readonly WallpaperEngine Wall = new();
    public readonly Config          Cfg  = new();

    private Pages.VideoSinglePage? _pgSingle;
    private Pages.VideoBatchPage?  _pgBatch;

    public MainWindow()
    {
        InitializeComponent();
        RestoreGeometry();

        // Restore auto-pause state from config (suppress the Checked event during init)
        ChkAutoPause.IsChecked = Cfg.GetBool("global/autoPause", false);
        if (ChkAutoPause.IsChecked == true)
            (System.Windows.Application.Current as App)?.StartAutoPause(Wall);

        Navigate("single");
    }

    // ── Silent auto-start (called on --silent startup, no window shown) ───────

    /// <summary>
    /// Reads the last-used settings from config and starts the wallpaper
    /// immediately, without showing the window or navigating to any page.
    /// Called when the app is launched with --silent (e.g. from Windows Startup).
    /// </summary>
    public void SilentAutoStart()
    {
        var file = Cfg.Get("single/file");
        if (string.IsNullOrEmpty(file) || !System.IO.File.Exists(file))
        {
            App.Log("[App] SilentAutoStart: no saved file, skipping");
            return;
        }

        int    monitorIdx = Cfg.GetInt   ("single/monitor",  0);
        string fit        = Cfg.Get      ("single/fit",      WallpaperEngine.FitOptions[1]);
        int    volume     = (int)Cfg.GetDouble("single/volume",   0);
        double speed      =      Cfg.GetDouble("single/speed",    0);
        int    bright     = (int)Cfg.GetDouble("single/bright",   0);
        int    contrast   = (int)Cfg.GetDouble("single/contrast", 0);
        int    panX       = (int)Cfg.GetDouble("single/panx",     0);
        int    panY       = (int)Cfg.GetDouble("single/pany",     0);

        App.Log($"[App] SilentAutoStart: \"{System.IO.Path.GetFileName(file)}\"");

        bool ok = Wall.Play(file,
            speedSlider:  speed,
            brightness:   bright,
            contrast:     contrast,
            panX:         panX,
            panY:         panY,
            fit:          fit,
            loop:         true,
            volume:       volume,
            monitorIndex: monitorIdx);

        SetStatus(ok
            ? $"> {System.IO.Path.GetFileName(file)}"
            : "! Could not start wallpaper", ok);

        App.Log($"[App] SilentAutoStart: {(ok ? "OK" : "FAILED")}");
    }

    // ── Status ───────────────────────────────────────────────────────────────

    public void SetStatus(string msg, bool playing)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text       = msg;
            StatusText.Foreground = playing
                ? (System.Windows.Media.Brush)FindResource("BrGreen")
                : (System.Windows.Media.Brush)FindResource("BrTextDim");
            StatusDot.Visibility  = playing ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private void Navigate(string key)
    {
        BtnVideoSingle.Style = (System.Windows.Style)FindResource("NavBtn");
        BtnVideoBatch .Style = (System.Windows.Style)FindResource("NavBtn");

        var active = (System.Windows.Style)FindResource("NavBtnActive");
        System.Windows.Controls.Page page;

        switch (key)
        {
            case "batch":
                page = (_pgBatch  ??= new Pages.VideoBatchPage());
                BtnVideoBatch.Style = active;
                break;
            default:
                page = (_pgSingle ??= new Pages.VideoSinglePage());
                BtnVideoSingle.Style = active;
                break;
        }

        PageFrame.Navigate(page);
    }

    private void OnNav_VideoSingle(object s, RoutedEventArgs e) => Navigate("single");
    private void OnNav_VideoBatch (object s, RoutedEventArgs e) => Navigate("batch");

    private void OnGitHubLink(object s, RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }

    // ── Stop All ─────────────────────────────────────────────────────────────

    private void OnStopAll(object s, RoutedEventArgs e) => StopAll();

    public void StopAll()
    {
        (_pgBatch as Pages.VideoBatchPage)?.StopScheduler();
        Wall.Stop();
        SetStatus("Idle  --  no wallpaper active", false);
    }

    // ── Auto-pause toggle ────────────────────────────────────────────────────

    private void OnAutoPauseChanged(object s, RoutedEventArgs e)
    {
        bool enabled = ChkAutoPause.IsChecked == true;
        Cfg.Set("global/autoPause", enabled);

        var app = System.Windows.Application.Current as App;
        if (enabled)
            app?.StartAutoPause(Wall);
        else
            app?.StopAutoPause();
    }

    // ── Minimize to tray on close ────────────────────────────────────────────

    private void OnClosing(object? s, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();

        // Trim working set when window hides — reduces tray-only RAM immediately
        App.TrimMemory();
    }

    // ── Geometry persistence ─────────────────────────────────────────────────

    private void RestoreGeometry()
    {
        var w = Cfg.GetDouble("window/width",  880);
        var h = Cfg.GetDouble("window/height", 580);
        var x = Cfg.GetDouble("window/left",   double.NaN);
        var y = Cfg.GetDouble("window/top",    double.NaN);
        if (w > 100) Width  = w;
        if (h > 100) Height = h;
        if (!double.IsNaN(x)) Left = x;
        if (!double.IsNaN(y)) Top  = y;
    }

    protected override void OnClosed(EventArgs e)
    {
        Cfg.Set("window/width",  Width);
        Cfg.Set("window/height", Height);
        Cfg.Set("window/left",   Left);
        Cfg.Set("window/top",    Top);
        (_pgBatch as Pages.VideoBatchPage)?.StopScheduler();
        Wall.Dispose();
        Cfg.SaveNow(); // flush any pending debounced save
        Cfg.Dispose();
        base.OnClosed(e);
    }
}
