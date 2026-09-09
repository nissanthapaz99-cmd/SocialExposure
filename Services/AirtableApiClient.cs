using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace SocialExposure.Services;

public sealed class AirtableApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly AirtableOptions _options;

    public AirtableApiClient(
        HttpClient httpClient,
        IOptions<AirtableOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<AirtableRecord>> GetAllRecordsAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var records = new List<AirtableRecord>();
        string? offset = null;

        do
        {
            var path = BuildTablePath(tableName) + "?pageSize=100";
            if (!string.IsNullOrWhiteSpace(offset))
                path += "&offset=" + Uri.EscapeDataString(offset);

            using var response = await SendAsync(
                () => CreateRequest(HttpMethod.Get, path),
                cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<AirtableListResponse>(
                JsonOptions,
                cancellationToken);

            if (payload?.Records != null)
                records.AddRange(payload.Records);

            offset = payload?.Offset;
        }
        while (!string.IsNullOrWhiteSpace(offset));

        return records;
    }

    public async Task<AirtableRecord> CreateRecordAsync(
        string tableName,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var payload = new AirtableWriteRequest
        {
            Typecast = true,
            Records = new[] { new AirtableWriteRecord { Fields = fields } }
        };

        using var response = await SendAsync(
            () => CreateJsonRequest(HttpMethod.Post, BuildTablePath(tableName), payload),
            cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<AirtableWriteResponse>(
            JsonOptions,
            cancellationToken);

        return result?.Records.FirstOrDefault()
            ?? throw new AirtableException("Airtable did not return the created record.");
    }

    public async Task<AirtableRecord> UpdateRecordAsync(
        string tableName,
        string recordId,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var payload = new AirtableWriteRequest
        {
            Typecast = true,
            Records = new[]
            {
                new AirtableWriteRecord
                {
                    Id = recordId,
                    Fields = fields
                }
            }
        };

        using var response = await SendAsync(
            () => CreateJsonRequest(HttpMethod.Patch, BuildTablePath(tableName), payload),
            cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<AirtableWriteResponse>(
            JsonOptions,
            cancellationToken);

        return result?.Records.FirstOrDefault()
            ?? throw new AirtableException("Airtable did not return the updated record.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var request = requestFactory();
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 3)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(attempt);
                response.Dispose();
                await Task.Delay(retryAfter, cancellationToken);
                continue;
            }

            if (response.IsSuccessStatusCode)
                return response;

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;
            response.Dispose();
            throw new AirtableException(
                $"Airtable returned HTTP {statusCode}: {TrimError(error)}");
        }

        throw new AirtableException("Airtable did not accept the request after three attempts.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _options.PersonalAccessToken.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private HttpRequestMessage CreateJsonRequest(
        HttpMethod method,
        string path,
        AirtableWriteRequest payload)
    {
        var request = CreateRequest(method, path);
        request.Content = JsonContent.Create(payload, options: JsonOptions);
        return request;
    }

    private string BuildTablePath(string tableName) =>
        $"{Uri.EscapeDataString(_options.BaseId.Trim())}/{Uri.EscapeDataString(tableName.Trim())}";

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
            throw new AirtableException("Airtable is not fully configured.");
    }

    private static string TrimError(string error)
    {
        var value = string.IsNullOrWhiteSpace(error) ? "Unknown error" : error.Trim();
        return value.Length <= 500 ? value : value[..500];
    }
}

public sealed class AirtableRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("createdTime")]
    public DateTimeOffset? CreatedTime { get; set; }

    [JsonPropertyName("fields")]
    public Dictionary<string, JsonElement> Fields { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AirtableException : Exception
{
    public AirtableException(string message)
        : base(message)
    {
    }
}

internal sealed class AirtableListResponse
{
    [JsonPropertyName("records")]
    public List<AirtableRecord> Records { get; set; } = new();

    [JsonPropertyName("offset")]
    public string? Offset { get; set; }
}

internal sealed class AirtableWriteRequest
{
    [JsonPropertyName("records")]
    public IReadOnlyList<AirtableWriteRecord> Records { get; set; } =
        Array.Empty<AirtableWriteRecord>();

    [JsonPropertyName("typecast")]
    public bool Typecast { get; set; }
}

internal sealed class AirtableWriteRecord
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, object?> Fields { get; set; } =
        new Dictionary<string, object?>();
}

internal sealed class AirtableWriteResponse
{
    [JsonPropertyName("records")]
    public List<AirtableRecord> Records { get; set; } = new();
}
