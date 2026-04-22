using System.Windows;
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
