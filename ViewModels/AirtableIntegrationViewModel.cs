using SocialExposure.Services;

namespace SocialExposure.ViewModels;

public sealed class AirtableIntegrationViewModel
{
    public bool Enabled { get; init; }

    public bool IsConfigured { get; init; }

    public bool IsRunning { get; init; }

    public string BaseId { get; init; } = string.Empty;

    public string ClientsTable { get; init; } = string.Empty;

    public string EventsTable { get; init; } = string.Empty;

    public string DesignsTable { get; init; } = string.Empty;

    public int SyncIntervalMinutes { get; init; }

    public AirtableSyncResult? LastResult { get; init; }
}
