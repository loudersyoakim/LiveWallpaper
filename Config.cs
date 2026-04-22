using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LiveWallpaper;

/// <summary>
/// Flat JSON config stored in %AppData%\LiveWallpaper\config.json.
/// Writes are debounced: after the last Set() call, a single file write
/// happens 400 ms later. This prevents hammering the disk on slider drag.
/// </summary>
public sealed class Config : IDisposable
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LiveWallpaper", "config.json");

    private readonly JsonObject _data;
    private System.Threading.Timer? _saveTimer;
    private readonly object _saveLock = new();

    public Config()
    {
        _data = Load();
    }

    private static JsonObject Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var text = File.ReadAllText(_path);
                return JsonNode.Parse(text)?.AsObject() ?? [];
            }
        }
        catch { /* ignore corrupt file */ }
        return [];
    }

    /// <summary>Schedules a save 400 ms from now, cancelling any pending save.</summary>
    public void Save()
    {
        lock (_saveLock)
        {
            _saveTimer?.Dispose();
            _saveTimer = new System.Threading.Timer(_ => DoSave(), null, 400, Timeout.Infinite);
        }
    }

    /// <summary>Saves immediately (call on app exit).</summary>
    public void SaveNow()
    {
        lock (_saveLock)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
        }
        DoSave();
    }

    private void DoSave()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string json;
            lock (_saveLock)
                json = _data.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            App.Log($"[Config] Save failed: {ex.Message}");
        }
    }

    public string Get(string key, string fallback = "")
        => _data[key]?.GetValue<string>() ?? fallback;

    public int GetInt(string key, int fallback = 0)
        => _data[key]?.GetValue<int>() ?? fallback;

    public bool GetBool(string key, bool fallback = false)
        => _data[key]?.GetValue<bool>() ?? fallback;

    public double GetDouble(string key, double fallback = 0)
        => _data[key]?.GetValue<double>() ?? fallback;

    public void Set(string key, string value) { lock (_saveLock) _data[key] = value; Save(); }
    public void Set(string key, int    value) { lock (_saveLock) _data[key] = value; Save(); }
    public void Set(string key, bool   value) { lock (_saveLock) _data[key] = value; Save(); }
    public void Set(string key, double value) { lock (_saveLock) _data[key] = value; Save(); }

    public void Dispose()
    {
        lock (_saveLock) { _saveTimer?.Dispose(); _saveTimer = null; }
    }
}
