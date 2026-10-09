using ProtonPassCliExtension.PassCli;

namespace ProtonPassCliExtension.Services;

/// <summary>Composition root shared by the pages and commands.</summary>
internal sealed class AppServices
{
    public AppServices()
    {
        Settings = new SettingsManager();
        Client = new PassCliClient(new SystemProcessRunner(), () => Settings.CliOptions);
        Cache = new ItemCache();
        Loader = new ItemLoader(Client);
        Clipboard = new ClipboardService(new WindowsClipboard());
        Settings.SettingsChanged += (_, _) => Cache.Invalidate();
    }

    public SettingsManager Settings { get; }

    public PassCliClient Client { get; }

    public ItemCache Cache { get; }

    public ItemLoader Loader { get; }

    public ClipboardService Clipboard { get; }
}
