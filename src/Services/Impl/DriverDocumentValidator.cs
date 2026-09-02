namespace SRC.Services.Impl;

/// <summary>
/// File rules for verification documents. The declared Content-Type is client-supplied and
/// therefore only advisory; the stored type comes from the file signature.
/// </summary>
public static class DriverDocumentValidator
{
    public const int MaxBytes = 5 * 1024 * 1024;

    private static readonly (byte[] Magic, string ContentType)[] Signatures =
    [
        (new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg"),
        (new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png"),
        (new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, "application/pdf") // %PDF-
    ];

    /// <summary>Returns the detected content type, or an error message describing why the file is refused.</summary>
    public static (string? ContentType, string? Error) Validate(byte[] content, string? fileName)
    {
        if (content.Length == 0)
            return (null, "The uploaded file is empty.");

        if (content.Length > MaxBytes)
            return (null, $"The file is {content.Length / 1024.0 / 1024.0:0.#} MB; the limit is 5 MB.");

        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
            return (null, "A file name of at most 255 characters is required.");

        foreach (var (magic, contentType) in Signatures)
        {
            if (content.Length >= magic.Length && content.AsSpan(0, magic.Length).SequenceEqual(magic))
                return (contentType, null);
        }

        return (null, "Only JPEG, PNG or PDF files are accepted. The file content does not match any of those formats.");
    }
}
