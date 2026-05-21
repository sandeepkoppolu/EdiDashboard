namespace EdiProcessor.Core.Models;

public class TradingPartner
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InterchangeId { get; set; } = string.Empty;   // ISA06
    public string InterchangeQualifier { get; set; } = string.Empty; // ISA05
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<EdiTransaction> Transactions { get; set; } = new List<EdiTransaction>();
}

public class EdiTransaction
{
    public int Id { get; set; }
    public int TradingPartnerId { get; set; }
    public TradingPartner TradingPartner { get; set; } = null!;
    public string TransactionType { get; set; } = string.Empty; // "837P","837I","837D","TA1","999"
    public string ControlNumber { get; set; } = string.Empty;
    public string RawContent { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Received"; // Received, Accepted, Rejected
    public string? ErrorDescription { get; set; }
    public ICollection<Claim837> Claims { get; set; } = new List<Claim837>();
    public ICollection<AcknowledgmentRecord> Acknowledgments { get; set; } = new List<AcknowledgmentRecord>();
}

public class Claim837
{
    public int Id { get; set; }
    public int EdiTransactionId { get; set; }
    public EdiTransaction EdiTransaction { get; set; } = null!;
    public string ClaimNumber { get; set; } = string.Empty;       // CLM01
    public string PatientName { get; set; } = string.Empty;
    public string PatientControlNumber { get; set; } = string.Empty; // NM1*QC
    public string ProviderName { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;         // NPI
    public string PayerId { get; set; } = string.Empty;
    public string PayerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime ServiceDateFrom { get; set; }
    public DateTime? ServiceDateTo { get; set; }
    public string ClaimType { get; set; } = string.Empty;          // "Professional","Institutional","Dental"
    public string Status { get; set; } = "Received";               // Received, Accepted, Rejected
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ServiceLine> ServiceLines { get; set; } = new List<ServiceLine>();
}

public class ServiceLine
{
    public int Id { get; set; }
    public int Claim837Id { get; set; }
    public Claim837 Claim837 { get; set; } = null!;
    public string ProcedureCode { get; set; } = string.Empty;
    public string? Modifier { get; set; }
    public decimal ChargedAmount { get; set; }
    public int Units { get; set; }
    public DateTime ServiceDate { get; set; }
    public string DiagnosisPointers { get; set; } = string.Empty;
}

public class AcknowledgmentRecord
{
    public int Id { get; set; }
    public int EdiTransactionId { get; set; }
    public EdiTransaction EdiTransaction { get; set; } = null!;
    public string AckType { get; set; } = string.Empty;  // "TA1", "999", or "277CA"
    public string ControlNumber { get; set; } = string.Empty;
    public string AcknowledgmentCode { get; set; } = string.Empty;  // A=Accepted, R=Rejected, E=Error
    public string? NoteCode { get; set; }
    public string? Description { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    // 999-specific
    public string? FunctionalGroupControlNumber { get; set; }
    public string? TransactionSetControlNumber { get; set; }
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Represents a single claim-level status entry from an inbound 277CA (Claim Acknowledgment).
/// One 277CA file can contain acknowledgments for many claims.
/// </summary>
public class Claim277CA
{
    public int Id { get; set; }

    /// <summary>FK → EdiTransaction row for the 277CA interchange.</summary>
    public int EdiTransactionId { get; set; }
    public EdiTransaction EdiTransaction { get; set; } = null!;

    /// <summary>FK → the matched 837 EdiTransaction, if found by submitter claim ID.</summary>
    public int? Linked837TransactionId { get; set; }
    public EdiTransaction? Linked837Transaction { get; set; }

    /// <summary>REF*1K – the submitter-assigned claim ID (matches CLM01 in the 837).</summary>
    public string SubmitterClaimId { get; set; } = string.Empty;

    /// <summary>REF*D9 – payer-assigned ICN/claim number.</summary>
    public string? PayerClaimNumber { get; set; }

    /// <summary>STC01-01 – status category code (e.g. A1, D0, P1, F1, R3).</summary>
    public string? StatusCategoryCode { get; set; }

    /// <summary>STC01-02 – status code within the category.</summary>
    public string? StatusCode { get; set; }

    /// <summary>Human-readable status description derived from STC codes.</summary>
    public string? StatusDescription { get; set; }

    /// <summary>STC02 – effective status date.</summary>
    public DateTime? StatusDate { get; set; }

    /// <summary>STC03 – action code (WQ=Pending, U=Finalized, etc.).</summary>
    public string? ActionCode { get; set; }

    /// <summary>STC04 – total claim charge amount.</summary>
    public decimal? TotalClaimChargeAmount { get; set; }

    /// <summary>STC05 – payment amount (when finalized).</summary>
    public decimal? PaymentAmount { get; set; }

    public string? PatientName { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderId { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

// ---- DTOs returned to the API / views ----

public class DashboardMetrics
{
    public int TotalClaims { get; set; }
    public int AcceptedClaims { get; set; }
    public int RejectedClaims { get; set; }
    public int PendingClaims { get; set; }
    public decimal TotalBilledAmount { get; set; }
    public int TotalTransactions { get; set; }
    public List<TradingPartnerMetric> ByTradingPartner { get; set; } = new();
    public List<DailyVolume> DailyVolumes { get; set; } = new();
    public List<ClaimTypeMetric> ByClaimType { get; set; } = new();
    public List<RecentTransaction> RecentTransactions { get; set; } = new();
}

public class TradingPartnerMetric
{
    public int TradingPartnerId { get; set; }
    public string TradingPartnerName { get; set; } = string.Empty;
    public int TotalClaims { get; set; }
    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public int Pending { get; set; }
    public decimal TotalAmount { get; set; }
    public double AcceptanceRate => TotalClaims == 0 ? 0 : Math.Round((double)Accepted / TotalClaims * 100, 1);
}

public class DailyVolume
{
    public DateTime Date { get; set; }
    public int Claims { get; set; }
    public int Accepted { get; set; }
    public int Rejected { get; set; }
}

public class ClaimTypeMetric
{
    public string ClaimType { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

public class RecentTransaction
{
    public int Id { get; set; }
    public string TradingPartner { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public string ControlNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public int ClaimCount { get; set; }
}

public class EdiUploadResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public string ControlNumber { get; set; } = string.Empty;
    public int ClaimsProcessed { get; set; }
    public List<string> Errors { get; set; } = new();
}
