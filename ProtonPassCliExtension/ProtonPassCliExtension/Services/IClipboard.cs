namespace ProtonPassCliExtension.Services;

internal interface IClipboard
{
    /// <summary>Counter that changes whenever the clipboard content changes.</summary>
    long SequenceNumber { get; }

    /// <summary>Places text on the clipboard, asking Windows to keep it out of history and cloud sync.</summary>
    void SetSensitiveText(string text);

    string? GetText();

    void Clear();
}
