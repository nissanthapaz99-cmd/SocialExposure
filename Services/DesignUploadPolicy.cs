using System.Text;

namespace SocialExposure.Services;

public static class DesignUploadPolicy
{
    public const long PhotoMaxBytes = 10L * 1024 * 1024;
    public const long VideoMaxBytes = 100L * 1024 * 1024;
    public const long RequestMaxBytes = 105L * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, (string Kind, string ContentType)> AllowedFiles =
        new Dictionary<string, (string Kind, string ContentType)>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ("photo", "image/jpeg"),
            [".jpeg"] = ("photo", "image/jpeg"),
            [".png"] = ("photo", "image/png"),
            [".webp"] = ("photo", "image/webp"),
            [".mp4"] = ("video", "video/mp4"),
            [".webm"] = ("video", "video/webm"),
            [".mov"] = ("video", "video/quicktime")
        };

    public static bool TryValidateMetadata(
        IFormFile file,
        out string extension,
        out string kind,
        out string error)
    {
        extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        kind = string.Empty;
        error = string.Empty;

        if (!AllowedFiles.TryGetValue(extension, out var allowed))
        {
            error = "Use JPG, PNG or WebP for photos, or MP4, WebM or MOV for videos.";
            return false;
        }

        if (!string.Equals(file.ContentType, allowed.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            error = "The file type does not match its extension.";
            return false;
        }

        kind = allowed.Kind;
        var limit = kind == "photo" ? PhotoMaxBytes : VideoMaxBytes;
        if (file.Length <= 0)
        {
            error = "Choose a non-empty file.";
            return false;
        }

        if (file.Length > limit)
        {
            error = kind == "photo"
                ? "Photos must be 10 MB or smaller."
                : "Videos must be 100 MB or smaller.";
            return false;
        }

        return true;
    }

    public static async Task<bool> HasValidSignatureAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken = default)
    {
        var header = new byte[16];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);

        return extension switch
        {
            ".jpg" or ".jpeg" => bytesRead >= 3 &&
                header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => bytesRead >= 8 && header[..8].SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => bytesRead >= 12 &&
                Encoding.ASCII.GetString(header, 0, 4) == "RIFF" &&
                Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
            ".mp4" or ".mov" => bytesRead >= 12 &&
                Encoding.ASCII.GetString(header, 4, 4) == "ftyp",
            ".webm" => bytesRead >= 4 && header[..4].SequenceEqual(
                new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            _ => false
        };
    }

    public static bool IsVideo(string? filePath) =>
        !string.IsNullOrWhiteSpace(filePath) &&
        Path.GetExtension(filePath).ToLowerInvariant() is ".mp4" or ".webm" or ".mov";

    public static string GetContentType(string filePath) =>
        AllowedFiles.TryGetValue(Path.GetExtension(filePath), out var allowed)
            ? allowed.ContentType
            : "application/octet-stream";
}
