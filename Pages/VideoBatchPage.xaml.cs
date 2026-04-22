using System.IO;
using System.Windows;
using System.Windows.Controls;
using LiveWallpaper.Engine;

namespace LiveWallpaper.Pages;

public partial class VideoBatchPage : Page
{
    private static readonly string[] VideoExts =
        [".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".flv", ".m4v", ".gif"];

    private static readonly (string Label, double Seconds)[] Intervals =
    [
        ("Loop current",  0),
        ("10 seconds",    10),
        ("30 seconds",    30),
        ("5 minutes",     5  * 60),
        ("10 minutes",    10 * 60),
        ("30 minutes",    30 * 60),
        ("1 hour",        3600),
        ("1 day",         86400),
    ];

    private MainWindow?      Main => Window.GetWindow(this) as MainWindow;
    private WallpaperEngine? Wall => Main?.Wall;
    private Config?          Cfg  => Main?.Cfg;

    private string?      _folder;
    private List<string> _files = [];
    private System.Threading.Timer? _schedTimer;
    private bool         _loaded;

    // Monitor list — populated once on first load
    private MonitorInfo[] _monitors = [];

    public VideoBatchPage()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += (_, _) => { /* preserve timer on tab switch */ };
    }

    private void OnLoaded(object s, RoutedEventArgs e)
    {
        // ── Monitor list ─────────────────────────────────────────────────────
        _monitors = WallpaperEngine.GetMonitors();
        if (CboMonitor.Items.Count == 0)
        {
            foreach (var m in _monitors)
                CboMonitor.Items.Add(m.Name);
            if (CboMonitor.Items.Count == 0)
                CboMonitor.Items.Add("Monitor 1 (Primary)");
        }
        int savedMonitor = Cfg?.GetInt("batch/monitor", 0) ?? 0;
        CboMonitor.SelectedIndex = Math.Clamp(savedMonitor, 0, CboMonitor.Items.Count - 1);

        // ── Fit list ─────────────────────────────────────────────────────────
        if (CboFit.Items.Count == 0)
            foreach (var opt in WallpaperEngine.FitOptions)
                CboFit.Items.Add(opt);

        var savedFit = Cfg?.Get("batch/fit", WallpaperEngine.FitOptions[1]);
        CboFit.SelectedItem = savedFit ?? WallpaperEngine.FitOptions[1];
        if (CboFit.SelectedItem == null && CboFit.Items.Count > 0)
            CboFit.SelectedIndex = 1;

        // ── Interval list ────────────────────────────────────────────────────
        if (CboInterval.Items.Count == 0)
            foreach (var (lbl, _) in Intervals)
                CboInterval.Items.Add(lbl);

        int intervalIdx = Cfg?.GetInt("batch/intervalIdx", 0) ?? 0;
        CboInterval.SelectedIndex = Math.Clamp(intervalIdx, 0, Intervals.Length - 1);

        // ── Sliders ──────────────────────────────────────────────────────────
        SldVolume.Value   = Cfg?.GetDouble("batch/volume",   0) ?? 0;
        SldSpeed.Value    = Cfg?.GetDouble("batch/speed",    0) ?? 0;
        SldBright.Value   = Cfg?.GetDouble("batch/bright",   0) ?? 0;
        SldContrast.Value = Cfg?.GetDouble("batch/contrast", 0) ?? 0;
        SldPanX.Value     = Cfg?.GetDouble("batch/panx",     0) ?? 0;
        SldPanY.Value     = Cfg?.GetDouble("batch/pany",     0) ?? 0;
        ChkShuffle.IsChecked = Cfg?.GetBool("batch/shuffle", false) ?? false;

        // ── Folder ───────────────────────────────────────────────────────────
        _folder = Cfg?.Get("batch/folder");
        if (!string.IsNullOrEmpty(_folder) && Directory.Exists(_folder))
            LoadFolder(_folder, silent: true);

        _loaded = true;
    }

    // ── Browse ────────────────────────────────────────────────────────────────

    private void OnBrowse(object s, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description  = "Select folder containing video files",
            SelectedPath = _folder ?? "",
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        LoadFolder(dlg.SelectedPath);
        Cfg?.Set("batch/folder", dlg.SelectedPath);
    }

    private void LoadFolder(string path, bool silent = false)
    {
        _folder = path;
        _files  = Directory.EnumerateFiles(path)
                      .Where(f => VideoExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                      .OrderBy(f => f)
                      .ToList();

        FolderLbl.Text       = path;
        FolderLbl.Foreground = (System.Windows.Media.Brush)FindResource("BrTextPri");
        CountLbl.Text        = $"{_files.Count} video file(s) found";

        FileList.Items.Clear();
        foreach (var f in _files)
            FileList.Items.Add(Path.GetFileName(f));

        BtnStart.IsEnabled = _files.Count > 0;

        if (!silent) App.Log($"[Sched] Folder loaded -- {_files.Count} files");
    }

    // ── Start ─────────────────────────────────────────────────────────────────

    private void OnStart(object s, RoutedEventArgs e)
    {
        if (_files.Count == 0) return;
        SaveSettings();
        StopScheduler();
        StartPlayback(fromStart: true);
        StartScheduler();
    }

    private void StartPlayback(bool fromStart)
    {
        int  monitorIdx = CboMonitor.SelectedIndex >= 0 ? CboMonitor.SelectedIndex : 0;
        bool shuffle    = ChkShuffle.IsChecked == true;
        var  fit        = CboFit.SelectedItem?.ToString() ?? WallpaperEngine.FitOptions[1];

        bool ok;
        if (fromStart)
        {
            int idx = FileList.SelectedIndex >= 0 ? FileList.SelectedIndex : 0;
            ok = Wall?.PlayPlaylist(_files, idx, shuffle,
                     SldSpeed.IntValue, SldBright.IntValue, SldContrast.IntValue,
                     SldPanX.IntValue, SldPanY.IntValue, fit,
                     SldVolume.IntValue, monitorIdx) ?? false;
        }
        else
        {
            ok = Wall?.PlaylistNext(
                     SldSpeed.IntValue, SldBright.IntValue, SldContrast.IntValue,
                     SldPanX.IntValue, SldPanY.IntValue, fit,
                     SldVolume.IntValue, monitorIdx) ?? false;
        }

        if (ok)
        {
            var name = Wall?.PlaylistCurrentName ?? "";
            Main?.SetStatus($">> {_files.Count} files  |  {name}", true);
        }
    }

    // ── Scheduler ─────────────────────────────────────────────────────────────

    private void StartScheduler()
    {
        var idx = CboInterval.SelectedIndex;
        if (idx < 0 || idx >= Intervals.Length) return;

        double secs = Intervals[idx].Seconds;
        if (secs <= 0)
        {
            UpdateSchedLabel("Loop only -- no auto-switch");
            return;
        }

        var span = TimeSpan.FromSeconds(secs);
        App.Log($"[Sched] Next switch in {Intervals[idx].Label}");
        UpdateSchedLabel($"Next switch in {Intervals[idx].Label}");

        _schedTimer = new System.Threading.Timer(_ =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                App.Log("[Sched] Switching to next video...");
                StartPlayback(fromStart: false);
            });
        }, null, span, span);
    }

    public void StopScheduler()
    {
        _schedTimer?.Dispose();
        _schedTimer = null;
        UpdateSchedLabel("");
    }

    private void UpdateSchedLabel(string text)
        => Dispatcher.InvokeAsync(() => SchedLbl.Text = text);

    // ── Manual prev / next / stop ─────────────────────────────────────────────

    private void OnPrev(object s, RoutedEventArgs e)
    {
        int monitorIdx = CboMonitor.SelectedIndex >= 0 ? CboMonitor.SelectedIndex : 0;
        Wall?.PlaylistPrev(SldSpeed.IntValue, SldBright.IntValue, SldContrast.IntValue,
                           SldPanX.IntValue, SldPanY.IntValue,
                           CboFit.SelectedItem?.ToString() ?? WallpaperEngine.FitOptions[1],
                           SldVolume.IntValue, monitorIdx);
        Main?.SetStatus($">> {_files.Count} files  |  {Wall?.PlaylistCurrentName}", true);
    }

    private void OnNext(object s, RoutedEventArgs e)
    {
        int monitorIdx = CboMonitor.SelectedIndex >= 0 ? CboMonitor.SelectedIndex : 0;
        Wall?.PlaylistNext(SldSpeed.IntValue, SldBright.IntValue, SldContrast.IntValue,
                           SldPanX.IntValue, SldPanY.IntValue,
                           CboFit.SelectedItem?.ToString() ?? WallpaperEngine.FitOptions[1],
                           SldVolume.IntValue, monitorIdx);
        Main?.SetStatus($">> {_files.Count} files  |  {Wall?.PlaylistCurrentName}", true);
    }

    private void OnStop(object s, RoutedEventArgs e)
    {
        StopScheduler();
        Wall?.Stop();
        Main?.SetStatus("Idle  --  no wallpaper active", false);
    }

    // ── Live property changes ─────────────────────────────────────────────────

    private void OnSettingChanged(object s, EventArgs e)
    {
        if (!_loaded) return;
        SaveSettings();
        if (Wall?.IsPlaying != true) return;

        Wall.SetVolume(SldVolume.IntValue);
        Wall.SetSpeed(SldSpeed.IntValue);
        Wall.SetBrightness(SldBright.IntValue);
        Wall.SetContrast(SldContrast.IntValue);
        Wall.SetPan(SldPanX.IntValue, SldPanY.IntValue);
        Wall.SetFit(CboFit.SelectedItem?.ToString() ?? WallpaperEngine.FitOptions[1]);
    }

    private void OnIntervalChanged(object s, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        Cfg?.Set("batch/intervalIdx", CboInterval.SelectedIndex);
        if (Wall?.IsPlaying == true)
        {
            StopScheduler();
            StartScheduler();
        }
    }

    private void OnOptionChanged(object s, RoutedEventArgs e)
        => Cfg?.Set("batch/shuffle", ChkShuffle.IsChecked == true);

    // ── Persist (debounced via Config) ────────────────────────────────────────

    private void SaveSettings()
    {
        Cfg?.Set("batch/monitor",     CboMonitor.SelectedIndex);
        Cfg?.Set("batch/fit",         CboFit.SelectedItem?.ToString() ?? "");
        Cfg?.Set("batch/volume",      SldVolume.Value);
        Cfg?.Set("batch/speed",       SldSpeed.Value);
        Cfg?.Set("batch/bright",      SldBright.Value);
        Cfg?.Set("batch/contrast",    SldContrast.Value);
        Cfg?.Set("batch/panx",        SldPanX.Value);
        Cfg?.Set("batch/pany",        SldPanY.Value);
        Cfg?.Set("batch/shuffle",     ChkShuffle.IsChecked == true);
        Cfg?.Set("batch/intervalIdx", CboInterval.SelectedIndex);
    }
}
