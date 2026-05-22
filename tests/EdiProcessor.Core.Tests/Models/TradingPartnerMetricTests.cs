using EdiProcessor.Core.Models;

namespace EdiProcessor.Core.Tests.Models;

public class TradingPartnerMetricTests
{
    [Fact]
    public void AcceptanceRate_ReturnsZero_WhenNoClaims()
    {
        var metric = new TradingPartnerMetric { TotalClaims = 0, Accepted = 5 };
        Assert.Equal(0, metric.AcceptanceRate);
    }

    [Fact]
    public void AcceptanceRate_RoundsToSingleDecimal()
    {
        var metric = new TradingPartnerMetric { TotalClaims = 3, Accepted = 2 };
        Assert.Equal(66.7, metric.AcceptanceRate);
    }
}
