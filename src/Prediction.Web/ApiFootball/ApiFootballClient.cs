using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Prediction.Web.ApiFootball;

public sealed class ApiFootballOptions
{
    public const string Section = "ApiFootball";

    public string BaseUrl { get; set; } = "https://v3.football.api-sports.io/";

    public string? ApiKey { get; set; }

    /// <summary>Nhà cái lấy kèo (8 = Bet365 trên API-Football).</summary>
    public int BookmakerId { get; set; } = 8;

    /// <summary>Khoảng nghỉ tối thiểu giữa hai request để không vượt giới hạn/phút.</summary>
    public int RequestDelayMs { get; set; } = 300;
}

public sealed class ApiFootballException(string message) : Exception(message);

/// <summary>Client cho API-Football v3 (api-sports.io).</summary>
public sealed class ApiFootballClient(HttpClient http, IOptions<ApiFootballOptions> options, ILogger<ApiFootballClient> logger)
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime _lastRequestUtc = DateTime.MinValue;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public int BookmakerId => options.Value.BookmakerId;

    /// <summary>Số request đã gửi bằng instance này.</summary>
    public int RequestCount { get; private set; }

    /// <summary>Số request còn lại trong ngày theo header gần nhất (null nếu chưa biết).</summary>
    public static int? RemainingToday { get; private set; }

    public static int? DailyLimit { get; private set; }

    public static DateTime? QuotaUpdatedUtc { get; private set; }

    public Task<StatusResponse?> GetStatusAsync(CancellationToken ct) => GetObjectAsync<StatusResponse>("status", ct);

    public async Task<LeagueItem?> GetLeagueAsync(int leagueId, CancellationToken ct) =>
        (await GetListAsync<LeagueItem>($"leagues?id={leagueId}", ct)).FirstOrDefault();

    public Task<List<LeagueItem>> SearchLeaguesAsync(string query, CancellationToken ct) =>
        GetListAsync<LeagueItem>($"leagues?search={Uri.EscapeDataString(query)}", ct);

    public Task<List<FixtureItem>> GetFixturesAsync(int leagueId, int season, CancellationToken ct) =>
        GetListAsync<FixtureItem>($"fixtures?league={leagueId}&season={season}", ct);

    /// <summary>Các trận đang đá của những giải cho trước.</summary>
    public Task<List<FixtureItem>> GetLiveFixturesAsync(IEnumerable<int> leagueIds, CancellationToken ct) =>
        GetListAsync<FixtureItem>($"fixtures?live={string.Join('-', leagueIds)}", ct);

    /// <summary>Trận theo id (tối đa 20 id mỗi lần).</summary>
    public Task<List<FixtureItem>> GetFixturesByIdsAsync(IEnumerable<int> fixtureIds, CancellationToken ct) =>
        GetListAsync<FixtureItem>($"fixtures?ids={string.Join('-', fixtureIds)}", ct);

    public Task<List<OddsItem>> GetLeagueOddsAsync(int leagueId, int season, CancellationToken ct) =>
        GetListAsync<OddsItem>($"odds?league={leagueId}&season={season}&bookmaker={BookmakerId}", ct);

    public Task<List<OddsItem>> GetFixtureOddsAsync(int fixtureId, CancellationToken ct) =>
        GetListAsync<OddsItem>($"odds?fixture={fixtureId}&bookmaker={BookmakerId}", ct);

    public Task<List<InjuryItem>> GetInjuriesAsync(int fixtureId, CancellationToken ct) =>
        GetListAsync<InjuryItem>($"injuries?fixture={fixtureId}", ct);

    public Task<List<CoachItem>> GetCoachesAsync(int teamId, CancellationToken ct) =>
        GetListAsync<CoachItem>($"coachs?team={teamId}", ct);

    public async Task<PredictionItem?> GetPredictionAsync(int fixtureId, CancellationToken ct) =>
        (await GetListAsync<PredictionItem>($"predictions?fixture={fixtureId}", ct)).FirstOrDefault();

    /// <summary>Gọi endpoint trả về danh sách, tự đi hết các trang.</summary>
    public async Task<List<T>> GetListAsync<T>(string pathAndQuery, CancellationToken ct)
    {
        var items = new List<T>();
        var page = 1;
        while (true)
        {
            var url = page == 1 ? pathAndQuery : $"{pathAndQuery}&page={page}";
            var envelope = await SendAsync(url, ct);
            if (envelope.Response.ValueKind == JsonValueKind.Array)
            {
                items.AddRange(envelope.Response.Deserialize<List<T>>(Json) ?? []);
            }

            if (envelope.Paging is null || envelope.Paging.Current >= envelope.Paging.Total)
            {
                return items;
            }

            page = envelope.Paging.Current + 1;
        }
    }

    public async Task<T?> GetObjectAsync<T>(string pathAndQuery, CancellationToken ct)
    {
        var envelope = await SendAsync(pathAndQuery, ct);
        return envelope.Response.ValueKind == JsonValueKind.Object ? envelope.Response.Deserialize<T>(Json) : default;
    }

    private async Task<ApiEnvelope> SendAsync(string pathAndQuery, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            throw new ApiFootballException("Chưa cấu hình ApiFootball:ApiKey.");
        }

        for (var attempt = 1; ; attempt++)
        {
            await ThrottleAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);
            request.Headers.Add("x-apisports-key", options.Value.ApiKey);
            RequestCount++;

            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt == 1)
            {
                logger.LogWarning("API-Football trả 429, chờ 60 giây rồi thử lại: {Path}", pathAndQuery);
                await Task.Delay(TimeSpan.FromSeconds(60), ct);
                continue;
            }

            ReadQuota(response);
            if (!response.IsSuccessStatusCode)
            {
                throw new ApiFootballException($"API-Football lỗi HTTP {(int)response.StatusCode} khi gọi {pathAndQuery}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var envelope = await JsonSerializer.DeserializeAsync<ApiEnvelope>(stream, Json, ct)
                ?? throw new ApiFootballException($"Phản hồi rỗng khi gọi {pathAndQuery}.");

            var error = ErrorText(envelope.Errors);
            if (error is not null)
            {
                throw new ApiFootballException($"API-Football: {error} ({pathAndQuery})");
            }

            return envelope;
        }
    }

    private static void ReadQuota(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("x-ratelimit-requests-remaining", out var remaining) && int.TryParse(remaining.FirstOrDefault(), out var r))
        {
            RemainingToday = r;
            QuotaUpdatedUtc = DateTime.UtcNow;
        }

        if (response.Headers.TryGetValues("x-ratelimit-requests-limit", out var limit) && int.TryParse(limit.FirstOrDefault(), out var l))
        {
            DailyLimit = l;
        }
    }

    /// <summary>API trả lỗi trong trường "errors" (mảng rỗng khi không lỗi, hoặc object {"token": "..."}).</summary>
    public static string? ErrorText(JsonElement errors) => errors.ValueKind switch
    {
        JsonValueKind.Object when errors.EnumerateObject().Any() =>
            string.Join("; ", errors.EnumerateObject().Select(p => $"{p.Name}: {p.Value}")),
        JsonValueKind.Array when errors.GetArrayLength() > 0 =>
            string.Join("; ", errors.EnumerateArray().Select(e => e.ToString())),
        _ => null,
    };

    private async Task ThrottleAsync(CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var wait = _lastRequestUtc.AddMilliseconds(options.Value.RequestDelayMs) - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, ct);
            }

            _lastRequestUtc = DateTime.UtcNow;
        }
        finally
        {
            Gate.Release();
        }
    }
}
