using System.Text;
using AiOrchestrator.BuildingBlocks.Files;

namespace AiOrchestrator.Infrastructure.Files;

/// <summary>
/// A file's bytes as text, capped, or <b>null</b> when it could not be read at all.
/// <para>
/// The one place that decides what a binary file is and how much of a large one is worth reading,
/// for a file on disk in the checkout that holds a PBI's branch. The caller must already have
/// established that the path belongs to the checkout; nothing here re-checks that.
/// </para>
/// </summary>
static class FileText
{
    /// <summary>
    /// Orca's number (<c>MAX_UNTRACKED_LINE_COUNT_BYTES</c>), and for its reason: an untracked file
    /// is very often a generated asset, and nobody is reading two megabytes of it in a diff pane.
    /// </summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>A NUL byte in the first chunk is git's own "this is binary" heuristic, and orca's.</summary>
    const int BinarySniffBytes = 8_192;

    /// <summary>
    /// Throws rather than substituting replacement characters. Undecodable bytes are the answer
    /// here, not something to paper over: what cannot be decoded cannot be displayed as text.
    /// </summary>
    static readonly Encoding Utf8Strict = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    public static BranchFileText? Read(string absolute)
    {
        byte[] bytes;
        long size;

        try
        {
            using var file = File.OpenRead(absolute);
            size = file.Length;
            var length = (int)Math.Min(file.Length, MaxBytes + 1L);
            bytes = new byte[length];
            var read = file.ReadAtLeast(bytes, length, throwOnEndOfStream: false);
            bytes = bytes[..read];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, BinarySniffBytes)) >= 0)
        {
            return new BranchFileText(string.Empty, IsBinary: true, Truncated: false, size);
        }

        var truncated = bytes.Length > MaxBytes;
        var readable = Math.Min(bytes.Length, MaxBytes);

        // A cut in the middle of a multi-byte character is not a broken file, so back off the
        // continuation bytes before deciding anything about what this is.
        if (truncated)
        {
            while (readable > 0 && (bytes[readable - 1] & 0b1100_0000) == 0b1000_0000)
            {
                readable--;
            }
        }

        try
        {
            return new BranchFileText(
                Utf8Strict.GetString(bytes, 0, readable),
                IsBinary: false,
                truncated,
                size
            );
        }
        catch (DecoderFallbackException)
        {
            // Binary, and the NUL sniff did not catch it. Three hundred bytes from /dev/urandom
            // contain no NUL about a third of the time, and one of them rendered as a pane of
            // mojibake with line numbers down the side. What can be shown as text is what decodes
            // as text — a stricter test than git's, and the right one for the side that has to draw it.
            return new BranchFileText(string.Empty, IsBinary: true, Truncated: false, size);
        }
    }
}
