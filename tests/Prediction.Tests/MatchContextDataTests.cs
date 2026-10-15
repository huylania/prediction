using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prediction.Core.Entities;
using Prediction.Web.ApiFootball;
using Prediction.Web.Services;

namespace Prediction.Tests;

public class MatchContextDataTests
{
    private static FixtureItem Sample() => JsonSerializer.Deserialize<FixtureItem>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "fixture-with-lineups.json")), ApiFootballClient.Json)!;

    [Fact]
    public void Maps_real_lineups_and_venue()
    {
        var item = Sample();
        var fixture = new Fixture();
        ApiMapper.Apply(fixture, item, "League", DateTime.UtcNow);
        Assert.Equal(11896, fixture.VenueId);
        Assert.Equal("Coventry Building Society Arena", fixture.VenueName);

        var lineup = new FixtureLineup();
        Assert.True(ApiMapper.ApplyLineup(lineup, item, DateTime.UtcNow));
        Assert.Equal(1557420, lineup.FixtureId);
        Assert.Equal("4-2-3-1", lineup.HomeFormation);
        Assert.StartsWith("C. Rushworth, M. van Ewijk", lineup.HomeStartXI);
        Assert.Equal(11, lineup.AwayStartXI.Split(", ").Length);
        Assert.Equal("Matthias Jaissle", lineup.AwayCoach);
    }

    [Fact]
    public void Lineup_not_published_yet()
    {
        var item = Sample();
        item.Lineups = [];
        Assert.False(ApiMapper.ApplyLineup(new FixtureLineup(), item, DateTime.UtcNow));

        item.Lineups = null;
        Assert.False(ApiMapper.ApplyLineup(new FixtureLineup(), item, DateTime.UtcNow));
    }

    [Fact]
    public void Maps_head_to_head_meeting()
    {
        var meeting = ApiMapper.ToMeeting(Sample());

        Assert.Equal("34-1346", meeting.PairKey);
        Assert.Equal(H2HMeeting.Key(34, 1346), H2HMeeting.Key(1346, 34));
        Assert.Equal(1346, meeting.HomeTeamId);
        Assert.Equal(4, meeting.HomeGoals);
        Assert.Equal(1, meeting.HalftimeHome);
        Assert.Equal("Premier League", meeting.LeagueName);
    }

    [Fact]
    public void Maps_venue()
    {
        var item = JsonSerializer.Deserialize<VenueItem>("""
            {"id": 11896, "name": "The Coventry Building Society Arena", "city": "Coventry, West Midlands", "country": "England", "capacity": 32609, "surface": "artificial turf"}
            """, ApiFootballClient.Json)!;
        var venue = new Venue();
        ApiMapper.Apply(venue, item, DateTime.UtcNow);

        Assert.Equal(32609, venue.Capacity);
        Assert.True(venue.IsArtificial);
    }

    private sealed class FakeHandler(Func<string, string> respond) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Urls.Add(url);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respond(url)) });
        }
    }

    private static (OpenMeteoClient Client, FakeHandler Handler) Meteo(Func<string, string> respond)
    {
        var handler = new FakeHandler(respond);
        return (new OpenMeteoClient(new HttpClient(handler), Options.Create(new OpenMeteoOptions()), NullLogger<OpenMeteoClient>.Instance), handler);
    }

    [Fact]
    public async Task Geocode_prefers_matching_country_and_reads_elevation()
    {
        var (client, handler) = Meteo(_ => """
            {"results": [
              {"name": "La Paz", "latitude": 24.14, "longitude": -110.31, "elevation": 27, "country": "Mexico"},
              {"name": "La Paz", "latitude": -16.5, "longitude": -68.15, "elevation": 3640, "country": "Bolivia"}
            ]}
            """);

        var point = await client.GeocodeAsync("La Paz", "Bolivia", default);

        Assert.NotNull(point);
        Assert.Equal(3640, point.ElevationM);
        Assert.Contains("name=La%20Paz", handler.Urls[0]);

        var england = await Meteo(_ => """{"results": [{"latitude": 52.4, "longitude": -1.5, "elevation": 92, "country": "United Kingdom"}]}""")
            .Client.GeocodeAsync("Coventry, West Midlands", "England", default);
        Assert.Equal(92, england!.ElevationM);
    }

    [Fact]
    public async Task Forecast_reads_kickoff_hour()
    {
        var (client, handler) = Meteo(_ => """
            {"latitude": 53.4, "longitude": -2.98, "hourly": {"time": ["2026-10-17T15:00"], "temperature_2m": [11.4], "relative_humidity_2m": [88],
             "precipitation": [2.6], "precipitation_probability": [80], "wind_speed_10m": [31.5], "weather_code": [63]}}
            """);

        var w = await client.ForecastAsync(53.4, -2.98, new DateTime(2026, 10, 17, 14, 30, 0, DateTimeKind.Utc), default);

        Assert.NotNull(w);
        Assert.Equal(11.4, w.TemperatureC);
        Assert.Equal(80, w.PrecipitationProbability);
        Assert.Equal(63, w.WeatherCode);
        Assert.Contains("start_hour=2026-10-17T15:00", handler.Urls[0]);
        Assert.Equal("mưa", OpenMeteoClient.Describe(63));
    }
}
