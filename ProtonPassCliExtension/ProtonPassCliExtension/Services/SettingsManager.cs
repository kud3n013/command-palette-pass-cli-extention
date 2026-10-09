using System;
using System.Globalization;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ProtonPassCliExtension.PassCli;

namespace ProtonPassCliExtension.Services;

/// <summary>Non-secret settings, persisted as JSON by the toolkit's <see cref="JsonSettingsManager"/>.</summary>
internal sealed class SettingsManager : JsonSettingsManager
{
    private const string PathId = "cliPath";
    private const string ClearDelayId = "clipboardClearSeconds";
    private const string CacheTtlId = "cacheTtlSeconds";

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(new TextSetting(PathId, "Path to pass-cli", "Leave as 'pass-cli' to use PATH, or enter a full path to pass-cli.exe.", "pass-cli"));
        Settings.Add(new TextSetting(ClearDelayId, "Clipboard clear delay (seconds)", "Clears copied secrets after this many seconds. 0 disables auto-clear.", "20"));
        Settings.Add(new TextSetting(CacheTtlId, "Item list cache (seconds)", "How long item titles are cached before reloading. Secrets are never cached.", "300"));

        LoadSettings();
        Settings.SettingsChanged += (_, _) =>
        {
            SaveSettings();
            Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? Changed;

    public ICommandSettings CommandSettings => Settings;

    public PassCliOptions CliOptions
    {
        get
        {
            var path = Settings.GetSetting<string>(PathId);
            return new PassCliOptions { ExecutablePath = string.IsNullOrWhiteSpace(path) ? "pass-cli" : path.Trim() };
        }
    }

    public TimeSpan ClipboardClearDelay => TimeSpan.FromSeconds(ReadSeconds(ClearDelayId, 20));

    public TimeSpan CacheTtl => TimeSpan.FromSeconds(ReadSeconds(CacheTtlId, 300));

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("ProtonPassCliExtension");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }

    private double ReadSeconds(string id, double fallback) =>
        double.TryParse(Settings.GetSetting<string>(id), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0
            ? v
            : fallback;
}
