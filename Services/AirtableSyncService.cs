using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialExposure.Data;
using SocialExposure.Models;
using EventEntity = SocialExposure.Models.Event;

namespace SocialExposure.Services;

public sealed class AirtableSyncService
{
    private static readonly string[] ClientFields =
    {
        "Website ID", "Full Name", "Email", "Company Name", "Phone Number",
        "Job Title", "Preferred Contact", "Account Status", "Created At"
    };

    private static readonly string[] EventFields =
    {
        "Website ID", "Event Name", "Client Name", "Client Email", "Description",
        "Start Date", "Deadline", "Status"
    };

    private static readonly string[] DesignFields =
    {
        "Website ID", "File Name", "File URL", "Description", "Version",
        "Uploaded At", "Event ID", "Client ID", "Status"
    };

    private readonly ApplicationDbContext _context;
    private readonly AirtableApiClient _api;
    private readonly AirtableOptions _options;
    private readonly AirtableSyncState _state;
    private readonly ILogger<AirtableSyncService> _logger;

    public AirtableSyncService(
        ApplicationDbContext context,
        AirtableApiClient api,
        IOptions<AirtableOptions> options,
        AirtableSyncState state,
        ILogger<AirtableSyncService> logger)
    {
        _context = context;
        _api = api;
        _options = options.Value;
        _state = state;
        _logger = logger;
    }

    public async Task<AirtableSyncResult> SyncAllAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new AirtableSyncResult();

        if (!_options.IsConfigured)
        {
            result.IsConfigured = false;
            result.Message = "Airtable is not configured yet.";
            result.CompletedAtUtc = DateTime.UtcNow;
            return result;
        }

        if (!_state.TryStart())
        {
            result.IsBusy = true;
            result.Message = "An Airtable sync is already running.";
            result.CompletedAtUtc = DateTime.UtcNow;
            return result;
        }

        var failedTables = 0;

        try
        {
            failedTables += await RunTableAsync(
                "Clients",
                () => SyncClientsAsync(result, cancellationToken),
                result);
            failedTables += await RunTableAsync(
                "Events",
                () => SyncEventsAsync(result, cancellationToken),
                result);
            failedTables += await RunTableAsync(
                "Designs",
                () => SyncDesignsAsync(result, cancellationToken),
                result);

            result.IsSuccessful = failedTables == 0;
            result.Message = result.IsSuccessful
                ? $"Airtable sync completed: {result.Imported} imported, {result.Exported} exported, {result.Conflicts} conflicts."
                : $"Airtable sync completed with errors in {failedTables} table(s).";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result.Message = "Airtable sync was cancelled.";
        }
        finally
        {
            result.CompletedAtUtc = DateTime.UtcNow;
            _state.Complete(result);
        }

        return result;
    }

    private async Task<int> RunTableAsync(
        string tableName,
        Func<Task> sync,
        AirtableSyncResult result)
    {
        try
        {
            await sync();
            return 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Airtable synchronization failed for {TableName}.", tableName);
            AddWarning(result, $"{tableName}: {exception.Message}");
            return 1;
        }
    }

    private async Task SyncClientsAsync(
        AirtableSyncResult result,
        CancellationToken cancellationToken)
    {
        var remoteRecords = await _api.GetAllRecordsAsync(
            _options.ClientsTable,
            cancellationToken);
        var localClients = await _context.Users
            .Where(user => user.Role == UserRoles.Client)
            .ToListAsync(cancellationToken);

        foreach (var remote in remoteRecords)
        {
            var websiteId = ReadInteger(remote, "Website ID");
            var email = ReadText(remote, "Email")?.Trim().ToLowerInvariant();
            var local = localClients.FirstOrDefault(user =>
                string.Equals(user.AirtableRecordId, remote.Id, StringComparison.Ordinal))
                ?? (websiteId.HasValue
                    ? localClients.FirstOrDefault(user => user.Id == websiteId.Value)
                    : null)
                ?? (!string.IsNullOrWhiteSpace(email)
                    ? localClients.FirstOrDefault(user =>
                        string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
                    : null);

            if (local == null)
            {
                if (!CanCreateClient(remote, out var reason))
                {
                    result.Skipped++;
                    AddWarning(result, $"Clients record {remote.Id} was skipped: {reason}");
                    continue;
                }

                local = new User
                {
                    Role = UserRoles.Client,
                    Email = email!,
                    FullName = ReadText(remote, "Full Name")!.Trim(),
                    CreatedAt = remote.CreatedTime?.UtcDateTime ?? DateTime.UtcNow,
                    IsActive = true,
                    IsVerified = false,
                    IsApproved = false,
                    AirtableRecordId = remote.Id
                };
                ApplyRemoteClient(remote, local);
                _context.Users.Add(local);
                localClients.Add(local);
                result.Imported++;
                continue;
            }

            local.AirtableRecordId = remote.Id;
            ImportWhenRemoteChanged(
                local.AirtableSyncHash,
                HashFields(BuildClientFields(local)),
                HashRemoteFields(remote, ClientFields),
                () => ApplyRemoteClient(remote, local),
                result);
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var client in localClients)
        {
            var fields = BuildClientFields(client);
            var localHash = HashFields(fields);
            var remote = FindRemote(remoteRecords, client.AirtableRecordId, client.Id);

            if (remote == null)
            {
                remote = await _api.CreateRecordAsync(
                    _options.ClientsTable,
                    fields,
                    cancellationToken);
                client.AirtableRecordId = remote.Id;
                result.Exported++;
            }
            else if (!string.Equals(
                         HashRemoteFields(remote, ClientFields),
                         localHash,
                         StringComparison.Ordinal))
            {
                await _api.UpdateRecordAsync(
                    _options.ClientsTable,
                    remote.Id,
                    fields,
                    cancellationToken);
                client.AirtableRecordId = remote.Id;
                result.Exported++;
            }

            client.AirtableSyncHash = localHash;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncEventsAsync(
        AirtableSyncResult result,
        CancellationToken cancellationToken)
    {
        var remoteRecords = await _api.GetAllRecordsAsync(
            _options.EventsTable,
            cancellationToken);
        var localEvents = await _context.Events.ToListAsync(cancellationToken);

        foreach (var remote in remoteRecords)
        {
            var websiteId = ReadInteger(remote, "Website ID");
            var local = localEvents.FirstOrDefault(item =>
                string.Equals(item.AirtableRecordId, remote.Id, StringComparison.Ordinal))
                ?? (websiteId.HasValue
                    ? localEvents.FirstOrDefault(item => item.Id == websiteId.Value)
                    : null);

            if (local == null)
            {
                if (!CanCreateEvent(remote, out var reason))
                {
                    result.Skipped++;
                    AddWarning(result, $"Events record {remote.Id} was skipped: {reason}");
                    continue;
                }

                local = new EventEntity
                {
                    EventName = ReadText(remote, "Event Name")!.Trim(),
                    ClientName = ReadText(remote, "Client Name")!.Trim(),
                    ClientEmail = ReadText(remote, "Client Email")!.Trim().ToLowerInvariant(),
                    Description = ReadText(remote, "Description")!.Trim(),
                    StartDate = ReadDate(remote, "Start Date")!.Value,
                    Deadline = ReadDate(remote, "Deadline")!.Value,
                    Status = ReadText(remote, "Status")?.Trim() ?? "Pending",
                    AirtableRecordId = remote.Id
                };
                _context.Events.Add(local);
                localEvents.Add(local);
                result.Imported++;
                continue;
            }

            local.AirtableRecordId = remote.Id;
            ImportWhenRemoteChanged(
                local.AirtableSyncHash,
                HashFields(BuildEventFields(local)),
                HashRemoteFields(remote, EventFields),
                () => ApplyRemoteEvent(remote, local),
                result);
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var eventItem in localEvents)
        {
            var fields = BuildEventFields(eventItem);
            var localHash = HashFields(fields);
            var remote = FindRemote(remoteRecords, eventItem.AirtableRecordId, eventItem.Id);

            if (remote == null)
            {
                remote = await _api.CreateRecordAsync(
                    _options.EventsTable,
                    fields,
                    cancellationToken);
                eventItem.AirtableRecordId = remote.Id;
                result.Exported++;
            }
            else if (!string.Equals(
                         HashRemoteFields(remote, EventFields),
                         localHash,
                         StringComparison.Ordinal))
            {
                await _api.UpdateRecordAsync(
                    _options.EventsTable,
                    remote.Id,
                    fields,
                    cancellationToken);
                eventItem.AirtableRecordId = remote.Id;
                result.Exported++;
            }

            eventItem.AirtableSyncHash = localHash;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncDesignsAsync(
        AirtableSyncResult result,
        CancellationToken cancellationToken)
    {
        var remoteRecords = await _api.GetAllRecordsAsync(
            _options.DesignsTable,
            cancellationToken);
        var localDesigns = await _context.Designs.ToListAsync(cancellationToken);
        var validEventIds = await _context.Events
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var validClientIds = await _context.Users
            .Where(user => user.Role == UserRoles.Client)
            .Select(user => user.Id)
            .ToHashSetAsync(cancellationToken);

        foreach (var remote in remoteRecords)
        {
            var websiteId = ReadInteger(remote, "Website ID");
            var local = localDesigns.FirstOrDefault(item =>
                string.Equals(item.AirtableRecordId, remote.Id, StringComparison.Ordinal))
                ?? (websiteId.HasValue
                    ? localDesigns.FirstOrDefault(item => item.Id == websiteId.Value)
                    : null);

            if (local == null)
            {
                if (!CanCreateDesign(remote, validEventIds, validClientIds, out var reason))
                {
                    result.Skipped++;
                    AddWarning(result, $"Designs record {remote.Id} was skipped: {reason}");
                    continue;
                }

                local = new Design
                {
                    FileName = ReadText(remote, "File Name")!.Trim(),
                    FilePath = SafeFileLocation(ReadText(remote, "File URL")),
                    Description = ReadText(remote, "Description")?.Trim(),
                    Version = ReadText(remote, "Version")?.Trim() ?? "v1.0",
                    UploadedAt = ReadDate(remote, "Uploaded At") ?? DateTime.UtcNow,
                    EventId = ReadInteger(remote, "Event ID")!.Value,
                    ClientId = ReadInteger(remote, "Client ID")!.Value,
                    Status = ReadText(remote, "Status")?.Trim() ?? "Pending Review",
                    AirtableRecordId = remote.Id
                };
                _context.Designs.Add(local);
                localDesigns.Add(local);
                result.Imported++;
                continue;
            }

            local.AirtableRecordId = remote.Id;
            ImportWhenRemoteChanged(
                local.AirtableSyncHash,
                HashFields(BuildDesignFields(local)),
                HashRemoteFields(remote, DesignFields),
                () => ApplyRemoteDesign(remote, local, validEventIds, validClientIds),
                result);
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var design in localDesigns)
        {
            var fields = BuildDesignFields(design);
            var localHash = HashFields(fields);
            var remote = FindRemote(remoteRecords, design.AirtableRecordId, design.Id);

            if (remote == null)
            {
                remote = await _api.CreateRecordAsync(
                    _options.DesignsTable,
                    fields,
                    cancellationToken);
                design.AirtableRecordId = remote.Id;
                result.Exported++;
            }
            else if (!string.Equals(
                         HashRemoteFields(remote, DesignFields),
                         localHash,
                         StringComparison.Ordinal))
            {
                await _api.UpdateRecordAsync(
                    _options.DesignsTable,
                    remote.Id,
                    fields,
                    cancellationToken);
                design.AirtableRecordId = remote.Id;
                result.Exported++;
            }

            design.AirtableSyncHash = localHash;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void ImportWhenRemoteChanged(
        string? lastSyncHash,
        string localHash,
        string remoteHash,
        Action applyRemote,
        AirtableSyncResult result)
    {
        if (string.IsNullOrWhiteSpace(lastSyncHash))
        {
            applyRemote();
            result.Imported++;
            return;
        }

        var localChanged = !string.Equals(lastSyncHash, localHash, StringComparison.Ordinal);
        var remoteChanged = !string.Equals(lastSyncHash, remoteHash, StringComparison.Ordinal);

        if (remoteChanged && !localChanged)
        {
            applyRemote();
            result.Imported++;
        }
        else if (remoteChanged && localChanged)
        {
            result.Conflicts++;
        }
    }

    private static IReadOnlyDictionary<string, object?> BuildClientFields(User client) =>
        new Dictionary<string, object?>
        {
            ["Website ID"] = client.Id,
            ["Full Name"] = client.FullName,
            ["Email"] = client.Email,
            ["Company Name"] = client.CompanyName,
            ["Phone Number"] = client.PhoneNumber,
            ["Job Title"] = client.JobTitle,
            ["Preferred Contact"] = client.PreferredContactMethod,
            ["Account Status"] = ClientStatus(client),
            ["Created At"] = FormatTimestamp(client.CreatedAt)
        };

    private static IReadOnlyDictionary<string, object?> BuildEventFields(EventEntity eventItem) =>
        new Dictionary<string, object?>
        {
            ["Website ID"] = eventItem.Id,
            ["Event Name"] = eventItem.EventName,
            ["Client Name"] = eventItem.ClientName,
            ["Client Email"] = eventItem.ClientEmail,
            ["Description"] = eventItem.Description,
            ["Start Date"] = eventItem.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["Deadline"] = eventItem.Deadline.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["Status"] = eventItem.Status
        };

    private static IReadOnlyDictionary<string, object?> BuildDesignFields(Design design) =>
        new Dictionary<string, object?>
        {
            ["Website ID"] = design.Id,
            ["File Name"] = design.FileName,
            ["File URL"] = design.FilePath,
            ["Description"] = design.Description,
            ["Version"] = design.Version,
            ["Uploaded At"] = FormatTimestamp(design.UploadedAt),
            ["Event ID"] = design.EventId,
            ["Client ID"] = design.ClientId,
            ["Status"] = design.Status
        };

    private static void ApplyRemoteClient(AirtableRecord remote, User client)
    {
        var fullName = ReadText(remote, "Full Name")?.Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
            client.FullName = fullName;

        client.CompanyName = EmptyToNull(ReadText(remote, "Company Name"));
        client.PhoneNumber = EmptyToNull(ReadText(remote, "Phone Number"));
        client.JobTitle = EmptyToNull(ReadText(remote, "Job Title"));

        var preferredContact = ReadText(remote, "Preferred Contact")?.Trim();
        client.PreferredContactMethod = preferredContact is "Email" or "Phone"
            ? preferredContact
            : "Email";
    }

    private static void ApplyRemoteEvent(AirtableRecord remote, EventEntity eventItem)
    {
        SetRequiredText(remote, "Event Name", value => eventItem.EventName = value);
        SetRequiredText(remote, "Client Name", value => eventItem.ClientName = value);

        var email = ReadText(remote, "Client Email")?.Trim().ToLowerInvariant();
        if (IsValidEmail(email))
            eventItem.ClientEmail = email!;

        SetRequiredText(remote, "Description", value => eventItem.Description = value);

        if (ReadDate(remote, "Start Date") is { } startDate)
            eventItem.StartDate = startDate;
        if (ReadDate(remote, "Deadline") is { } deadline)
            eventItem.Deadline = deadline;

        SetRequiredText(remote, "Status", value => eventItem.Status = value);
    }

    private static void ApplyRemoteDesign(
        AirtableRecord remote,
        Design design,
        IReadOnlySet<int> validEventIds,
        IReadOnlySet<int> validClientIds)
    {
        SetRequiredText(remote, "File Name", value => design.FileName = value);
        design.Description = EmptyToNull(ReadText(remote, "Description"));
        SetRequiredText(remote, "Version", value => design.Version = value);
        SetRequiredText(remote, "Status", value => design.Status = value);

        if (ReadInteger(remote, "Event ID") is { } eventId && validEventIds.Contains(eventId))
            design.EventId = eventId;
        if (ReadInteger(remote, "Client ID") is { } clientId && validClientIds.Contains(clientId))
            design.ClientId = clientId;
    }

    private static bool CanCreateClient(AirtableRecord remote, out string reason)
    {
        if (string.IsNullOrWhiteSpace(ReadText(remote, "Full Name")))
        {
            reason = "Full Name is required.";
            return false;
        }

        if (!IsValidEmail(ReadText(remote, "Email")))
        {
            reason = "a valid Email is required.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool CanCreateEvent(AirtableRecord remote, out string reason)
    {
        if (string.IsNullOrWhiteSpace(ReadText(remote, "Event Name")) ||
            string.IsNullOrWhiteSpace(ReadText(remote, "Client Name")) ||
            string.IsNullOrWhiteSpace(ReadText(remote, "Description")) ||
            !IsValidEmail(ReadText(remote, "Client Email")) ||
            ReadDate(remote, "Start Date") == null ||
            ReadDate(remote, "Deadline") == null)
        {
            reason = "Event Name, Client Name, Client Email, Description, Start Date and Deadline are required.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool CanCreateDesign(
        AirtableRecord remote,
        IReadOnlySet<int> validEventIds,
        IReadOnlySet<int> validClientIds,
        out string reason)
    {
        var eventId = ReadInteger(remote, "Event ID");
        var clientId = ReadInteger(remote, "Client ID");

        if (string.IsNullOrWhiteSpace(ReadText(remote, "File Name")) ||
            !eventId.HasValue || !validEventIds.Contains(eventId.Value) ||
            !clientId.HasValue || !validClientIds.Contains(clientId.Value))
        {
            reason = "File Name and valid website Event ID and Client ID values are required.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static AirtableRecord? FindRemote(
        IReadOnlyList<AirtableRecord> records,
        string? recordId,
        int websiteId) =>
        (!string.IsNullOrWhiteSpace(recordId)
            ? records.FirstOrDefault(record =>
                string.Equals(record.Id, recordId, StringComparison.Ordinal))
            : null)
        ?? records.FirstOrDefault(record => ReadInteger(record, "Website ID") == websiteId);

    private static string HashFields(IReadOnlyDictionary<string, object?> fields)
    {
        var canonical = string.Join(
            "\n",
            fields.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={CanonicalValue(pair.Key, pair.Value)}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string HashRemoteFields(AirtableRecord record, IEnumerable<string> fields)
    {
        var canonical = string.Join(
            "\n",
            fields.OrderBy(field => field, StringComparer.Ordinal)
                .Select(field =>
                    $"{field}={CanonicalValue(field, ReadField(record, field))}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string CanonicalValue(string fieldName, object? value)
    {
        if (value == null)
            return string.Empty;

        if (value is JsonElement json)
        {
            if (json.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return string.Empty;
            if (json.ValueKind == JsonValueKind.String)
                return CanonicalText(fieldName, json.GetString());
            if (json.ValueKind == JsonValueKind.Number && json.TryGetDecimal(out var number))
                return number.ToString(CultureInfo.InvariantCulture);
            if (json.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return json.GetBoolean() ? "true" : "false";
            return json.GetRawText();
        }

        if (value is DateTime dateTime)
            return CanonicalText(fieldName, dateTime.ToString("O", CultureInfo.InvariantCulture));
        if (value is DateTimeOffset dateTimeOffset)
            return CanonicalText(fieldName, dateTimeOffset.ToString("O", CultureInfo.InvariantCulture));
        if (value is IFormattable formattable && value is not string)
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

        return CanonicalText(fieldName, value.ToString());
    }

    private static string CanonicalText(string fieldName, string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (fieldName is "Start Date" or "Deadline" &&
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (fieldName is "Created At" or "Uploaded At" &&
            DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            return timestamp.UtcDateTime.ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture);
        }

        return text;
    }

    private static JsonElement? ReadField(AirtableRecord record, string fieldName)
    {
        if (record.Fields.TryGetValue(fieldName, out var exact))
            return exact;

        foreach (var field in record.Fields)
        {
            if (string.Equals(field.Key, fieldName, StringComparison.OrdinalIgnoreCase))
                return field.Value;
        }

        return null;
    }

    private static string? ReadText(AirtableRecord record, string fieldName)
    {
        var field = ReadField(record, fieldName);
        if (!field.HasValue || field.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return field.Value.ValueKind == JsonValueKind.String
            ? field.Value.GetString()
            : field.Value.ToString();
    }

    private static int? ReadInteger(AirtableRecord record, string fieldName)
    {
        var field = ReadField(record, fieldName);
        if (!field.HasValue)
            return null;

        if (field.Value.ValueKind == JsonValueKind.Number && field.Value.TryGetInt32(out var number))
            return number;

        return int.TryParse(ReadText(record, fieldName), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static DateTime? ReadDate(AirtableRecord record, string fieldName) =>
        DateTime.TryParse(
            ReadText(record, fieldName),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var date)
            ? date
            : null;

    private static void SetRequiredText(
        AirtableRecord record,
        string fieldName,
        Action<string> setter)
    {
        var value = ReadText(record, fieldName)?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
            setter(value);
    }

    private static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && new EmailAddressAttribute().IsValid(email.Trim());

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? SafeFileLocation(string? value)
    {
        var path = value?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return path;
        return Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri.ToString()
            : null;
    }

    private static string ClientStatus(User user)
    {
        if (!user.IsVerified)
            return "Unverified";
        if (!user.IsApproved)
            return user.IsActive ? "Pending approval" : "Rejected";
        return user.IsActive ? "Active" : "Suspended";
    }

    private static string? FormatTimestamp(DateTime? value) =>
        value?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static void AddWarning(AirtableSyncResult result, string warning)
    {
        if (result.Warnings.Count < 20)
            result.Warnings.Add(warning);
    }
}
