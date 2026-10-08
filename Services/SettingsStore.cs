using System;
using System.IO;
using System.Text.Json;
using DesktopWeather.Models;

namespace DesktopWeather.Services;

public sealed class SettingsStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    public SettingsStore(string directory) => _path = Path.Combine(directory, "settings.json");
    public string? LastError { get; private set; }

    public WeatherSettings Load()
    {
        try
        {
            LastError = null;
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<WeatherSettings>(File.ReadAllText(_path), _options) ?? new WeatherSettings()
                : new WeatherSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LastError = "设置文件无法读取，已使用默认设置。";
            return new WeatherSettings();
        }
    }

    public bool Save(WeatherSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(settings, _options));
            File.Move(_path + ".tmp", _path, true);
            LastError = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastError = "设置未能保存，请检查软件所在文件夹的写入权限。";
            return false;
        }
    }
}
