using Prediction.Web.Services;

namespace Prediction.Tests;

public class SideInfoParsingTests
{
    [Fact]
    public void Parses_fenced_json_after_text()
    {
        var parsed = SideInfoService.Parse("""
            Mình đã tra cứu:
            ```json
            {"rivalry": "Derby Merseyside", "altitude_m": 12, "lines": [{"fixture_id": 5, "ah_home_line": -0.5}],}
            ```
            """);

        Assert.NotNull(parsed);
        Assert.Equal("Derby Merseyside", parsed.Value.GetProperty("rivalry").GetString());
        Assert.Equal(1, parsed.Value.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public void Parses_bare_json_and_rejects_garbage()
    {
        Assert.NotNull(SideInfoService.Parse("""Kết quả: {"news": null, "sources": []} hết."""));
        Assert.Null(SideInfoService.Parse("Không tìm thấy thông tin."));
        Assert.Null(SideInfoService.Parse("```json\n{ hỏng \n```"));
    }

    [Theory]
    [InlineData("claude-opus-5-5", 4, 20)]
    [InlineData("claude-sonnet-5-5", 2, 10)]
    [InlineData("claude-haiku-5-5", 1, 5)]
    public void Prices_by_model_family(string model, int input, int output)
    {
        var (i, o) = SideInfoService.Prices(model);
        Assert.Equal(input, i);
        Assert.Equal(output, o);
    }
}
