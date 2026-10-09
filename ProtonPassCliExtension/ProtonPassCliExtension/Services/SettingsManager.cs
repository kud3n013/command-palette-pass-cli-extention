using System;
using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ProtonPassCliExtension.PassCli;

namespace ProtonPassCliExtension.Services;

internal sealed class SettingsManager
{
    private const string PathId = "cliPath";
    private const string ClearDelayId = "clipboardClearSeconds";
    private const string CacheTtlId = "cacheTtlSeconds";

    private readonly Settings _settings = new();

    public SettingsManager()
    {
        _settings.Add(new TextSetting(PathId, "Path to pass-cli", "Leave as 'pass-cli' to use PATH, or enter a full path to pass-cli.exe.", "pass-cli"));
        _settings.Add(new TextSetting(ClearDelayId, "Clipboard clear delay (seconds)", "Clears copied secrets after this many seconds. 0 disables auto-clear.", "20"));
        _settings.Add(new TextSetting(CacheTtlId, "Item list cache (seconds)", "How long item titles are cached before reloading. Secrets are never cached.", "300"));
        _settings.SettingsChanged += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? SettingsChanged;

    public ICommandSettings Settings => _settings;

    public PassCliOptions CliOptions => new()
    {
        ExecutablePath = string.IsNullOrWhiteSpace(_settings.GetSetting<string>(PathId)) ? "pass-cli" : _settings.GetSetting<string>(PathId)!.Trim(),
    };

    public TimeSpan ClipboardClearDelay => TimeSpan.FromSeconds(ReadSeconds(ClearDelayId, 20));

    public TimeSpan CacheTtl => TimeSpan.FromSeconds(ReadSeconds(CacheTtlId, 300));

    private double ReadSeconds(string id, double fallback) =>
        double.TryParse(_settings.GetSetting<string>(id), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0
            ? v
            : fallback;
}
