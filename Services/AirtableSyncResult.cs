namespace SocialExposure.Services;

public sealed class AirtableSyncResult
{
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public bool IsConfigured { get; set; } = true;

    public bool IsSuccessful { get; set; }

    public bool IsBusy { get; set; }

    public int Imported { get; set; }

    public int Exported { get; set; }

    public int Conflicts { get; set; }

    public int Skipped { get; set; }

    public List<string> Warnings { get; } = new();

    public string Message { get; set; } = string.Empty;
}
