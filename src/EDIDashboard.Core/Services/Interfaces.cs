using EDIDashboard.Core.Models;

namespace EDIDashboard.Core.Services;

public interface IEdiProcessingService
{
    Task<EdiUploadResult> ProcessEdiFileAsync(string rawContent, int tradingPartnerId, string? fileName = null);
}

public interface IMetricsService
{
    Task<DashboardMetrics> GetDashboardMetricsAsync(int? tradingPartnerId = null, DateTime? from = null, DateTime? to = null);
}

public interface ITradingPartnerService
{
    Task<List<TradingPartner>> GetAllAsync();
    Task<TradingPartner?> GetByIdAsync(int id);
    Task<TradingPartner?> GetByInterchangeIdAsync(string interchangeId);
    Task<TradingPartner> CreateAsync(TradingPartner partner);
    Task<TradingPartner> UpdateAsync(TradingPartner partner);
    Task DeleteAsync(int id);
}

public interface IClaimRepository
{
    Task<List<Claim837>> GetClaimsAsync(int? tradingPartnerId, DateTime? from, DateTime? to, int page, int pageSize);
    Task<int> GetClaimsCountAsync(int? tradingPartnerId, DateTime? from, DateTime? to);
    Task<Claim837?> GetByIdAsync(int id);
    Task<List<AcknowledgmentRecord>> GetAcknowledgmentsAsync(int? tradingPartnerId, int page, int pageSize);
}
