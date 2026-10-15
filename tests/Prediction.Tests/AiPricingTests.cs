using Prediction.Web.Services;

namespace Prediction.Tests;

public class AiPricingTests
{
    [Fact]
    public void Cost_counts_cache_and_web_search()
    {
        // Opus: vào $4, ra $20 / triệu token; ghi cache 1 giờ ×2, đọc cache ×0.1; tra web $0.01/lượt.
        var cost = AiPricing.Cost("claude-opus-5-5", input: 1000, output: 500, cacheRead: 2000, cacheWrite: 3000, searches: 2, cacheWrite1h: true);
        Assert.Equal(0.0588m, cost);

        // Sonnet, ghi cache 5 phút ×1.25.
        Assert.Equal(0.013m, AiPricing.Cost("claude-sonnet-5-5", 1000, 1000, 0, 400, 0));
    }
}
