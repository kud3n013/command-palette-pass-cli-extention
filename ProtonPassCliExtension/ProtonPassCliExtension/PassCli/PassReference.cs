namespace ProtonPassCliExtension.PassCli;

internal static class PassReference
{
    // Item IDs are only unique together with the share ID, and may start with '-'.
    // A pass:// reference is always a single argv entry that cannot be mistaken for a flag.
    public static string Build(string shareId, string itemId, string? field = null) =>
        field is null ? $"pass://{shareId}/{itemId}" : $"pass://{shareId}/{itemId}/{field}";
}
