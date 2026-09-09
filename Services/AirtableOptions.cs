namespace SocialExposure.Services;

public sealed class AirtableOptions
{
    public const string SectionName = "Airtable";

    public bool Enabled { get; set; }

    public string PersonalAccessToken { get; set; } = string.Empty;

    public string BaseId { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = "https://api.airtable.com/v0/";

    public string ClientsTable { get; set; } = "Clients";

    public string EventsTable { get; set; } = "Events";

    public string DesignsTable { get; set; } = "Designs";

    public int SyncIntervalMinutes { get; set; } = 5;

    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(PersonalAccessToken) &&
        !string.IsNullOrWhiteSpace(BaseId) &&
        !string.IsNullOrWhiteSpace(ClientsTable) &&
        !string.IsNullOrWhiteSpace(EventsTable) &&
        !string.IsNullOrWhiteSpace(DesignsTable);
}
