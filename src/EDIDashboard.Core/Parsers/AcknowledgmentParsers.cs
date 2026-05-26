using EDIDashboard.Core.Models;

namespace EDIDashboard.Core.Parsers;

/// <summary>
/// Parses TA1 (Interchange Acknowledgment) segments.
/// TA1 acknowledges the ISA/IEA envelope. One TA1 per interchange.
/// </summary>
public class Ta1Parser
{
    private char _segmentTerminator = '~';
    private char _elementSeparator = '*';

    public (EdiTransaction transaction, AcknowledgmentRecord ack) Parse(string rawContent, int tradingPartnerId)
    {
        DetectDelimiters(rawContent);

        var segments = rawContent
            .Split(_segmentTerminator, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        var transaction = new EdiTransaction
        {
            TradingPartnerId = tradingPartnerId,
            TransactionType = "TA1",
            RawContent = rawContent,
            ReceivedAt = DateTime.UtcNow,
            Status = "Received"
        };

        var ack = new AcknowledgmentRecord
        {
            AckType = "TA1",
            ReceivedAt = DateTime.UtcNow
        };

        foreach (var seg in segments)
        {
            var el = seg.Split(_elementSeparator);
            if (el.Length == 0) continue;

            switch (el[0].Trim())
            {
                case "ISA":
                    transaction.ControlNumber = el.Length > 13 ? el[13].Trim() : "";
                    break;

                case "TA1":
                    // TA1*000000905*030101*1234*A*000
                    ack.ControlNumber = el.Length > 1 ? el[1].Trim() : "";
                    ack.AcknowledgmentCode = el.Length > 4 ? el[4].Trim() : "";
                    ack.NoteCode = el.Length > 5 ? el[5].Trim() : "";
                    ack.Description = GetTa1Description(ack.AcknowledgmentCode, ack.NoteCode);
                    transaction.ControlNumber = ack.ControlNumber;
                    transaction.Status = MapAckCodeToStatus(ack.AcknowledgmentCode);
                    break;
            }
        }

        return (transaction, ack);
    }

    private string GetTa1Description(string ackCode, string? noteCode) =>
        ackCode switch
        {
            "A" => "Interchange accepted",
            "E" => $"Interchange accepted with errors (Note: {noteCode})",
            "R" => $"Interchange rejected (Note: {noteCode}) - {GetTa1NoteDescription(noteCode)}",
            _ => $"Unknown acknowledgment code: {ackCode}"
        };

    private string GetTa1NoteDescription(string? code) =>
        code switch
        {
            "000" => "No error",
            "001" => "ISA01 – Invalid authorization information qualifier",
            "002" => "ISA02 – Invalid authorization information",
            "003" => "ISA03 – Invalid security information qualifier",
            "004" => "ISA04 – Invalid security information",
            "005" => "ISA05 – Invalid interchange ID qualifier (sender)",
            "006" => "ISA06 – Invalid interchange sender ID",
            "007" => "ISA07 – Invalid interchange ID qualifier (receiver)",
            "008" => "ISA08 – Invalid interchange receiver ID",
            "009" => "ISA09 – Invalid interchange date",
            "010" => "ISA10 – Invalid interchange time",
            "011" => "ISA11 – Invalid interchange control standards identifier",
            "012" => "ISA12 – Invalid interchange control version number",
            "013" => "ISA13 – Invalid interchange control number",
            "014" => "ISA14 – Invalid acknowledgment requested",
            "015" => "ISA15 – Invalid test indicator",
            "022" => "Invalid control structure",
            "023" => "Improper end of file",
            "024" => "Invalid interchange content",
            "025" => "Duplicate interchange control number",
            "026" => "Invalid data element separator",
            "027" => "Invalid component element separator",
            _ => "Refer to payer specifications"
        };

    private string MapAckCodeToStatus(string code) =>
        code switch { "A" => "Accepted", "E" => "Accepted", "R" => "Rejected", _ => "Received" };

    private void DetectDelimiters(string raw)
    {
        if (raw.Length >= 106) { _elementSeparator = raw[3]; _segmentTerminator = raw[105]; }
    }
}

/// <summary>
/// Parses 999 (Functional Acknowledgment) transactions.
/// 999 acknowledges individual functional groups / transaction sets within an interchange.
/// </summary>
public class Edi999Parser
{
    private char _segmentTerminator = '~';
    private char _elementSeparator = '*';

    public (EdiTransaction transaction, List<AcknowledgmentRecord> acks) Parse(string rawContent, int tradingPartnerId)
    {
        DetectDelimiters(rawContent);

        var segments = rawContent
            .Split(_segmentTerminator, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        var transaction = new EdiTransaction
        {
            TradingPartnerId = tradingPartnerId,
            TransactionType = "999",
            RawContent = rawContent,
            ReceivedAt = DateTime.UtcNow,
            Status = "Received"
        };

        var acks = new List<AcknowledgmentRecord>();
        AcknowledgmentRecord? currentAck = null;
        var errors = new List<string>();

        foreach (var seg in segments)
        {
            var el = seg.Split(_elementSeparator);
            if (el.Length == 0) continue;

            switch (el[0].Trim())
            {
                case "ISA":
                    transaction.ControlNumber = el.Length > 13 ? el[13].Trim() : "";
                    break;

                case "ST":
                    // ST*999*0001
                    break;

                case "AK1":
                    // AK1*HC*000000001 – opens a functional group ack
                    currentAck = new AcknowledgmentRecord
                    {
                        AckType = "999",
                        ReceivedAt = DateTime.UtcNow,
                        FunctionalGroupControlNumber = el.Length > 2 ? el[2].Trim() : "",
                        ControlNumber = transaction.ControlNumber
                    };
                    errors.Clear();
                    break;

                case "AK2":
                    // AK2*837*0001 – transaction set being acked
                    if (currentAck != null)
                        currentAck.TransactionSetControlNumber = el.Length > 2 ? el[2].Trim() : "";
                    break;

                case "IK3":
                    // IK3 – data segment note (error)
                    if (currentAck != null)
                        errors.Add($"Segment error at {(el.Length > 1 ? el[1] : "?")} (pos {(el.Length > 3 ? el[3] : "?")})");
                    break;

                case "IK4":
                    // IK4 – element-level error
                    if (currentAck != null)
                        errors.Add($"Element error: {(el.Length > 3 ? Get999ErrorCode(el[3]) : "")}");
                    break;

                case "IK5":
                case "AK5":
                    // IK5/AK5*A or *R*xxx – transaction set acknowledgment
                    if (currentAck == null)
                    {
                        // Some partner files omit AK1/AK2 details; still emit a 999 acknowledgment row.
                        currentAck = new AcknowledgmentRecord
                        {
                            AckType = "999",
                            ReceivedAt = DateTime.UtcNow,
                            ControlNumber = transaction.ControlNumber
                        };
                    }

                    currentAck.AcknowledgmentCode = el.Length > 1 ? el[1].Trim() : "";
                    currentAck.ErrorCode = el.Length > 2 ? el[2].Trim() : null;
                    currentAck.Description = BuildAk5Description(currentAck.AcknowledgmentCode, errors);
                    acks.Add(currentAck);
                    currentAck = null;
                    break;

                case "AK9":
                    // AK9*A*1*1*1 – functional group summary
                    var groupCode = el.Length > 1 ? el[1].Trim() : "A";
                    transaction.Status = groupCode switch
                    {
                        "A" => "Accepted",
                        "E" => "Accepted",
                        "R" => "Rejected",
                        "P" => "Accepted",
                        _ => "Received"
                    };
                    break;
            }
        }

        return (transaction, acks);
    }

    private string BuildAk5Description(string code, List<string> errors)
    {
        var baseDesc = code switch
        {
            "A" => "Transaction set accepted",
            "E" => "Transaction set accepted with errors",
            "R" => "Transaction set rejected",
            "M" => "Transaction set rejected - manual review required",
            "W" => "Transaction set rejected - not in agreement",
            "X" => "Transaction set rejected - cannot be processed",
            _ => $"Unknown disposition: {code}"
        };
        if (errors.Count > 0)
            return $"{baseDesc}. Errors: {string.Join("; ", errors)}";
        return baseDesc;
    }

    private string Get999ErrorCode(string code) =>
        code switch
        {
            "1" => "Mandatory data element missing",
            "2" => "Conditional required data element missing",
            "3" => "Too many data elements",
            "4" => "Data element too short",
            "5" => "Data element too long",
            "6" => "Invalid character in data element",
            "7" => "Invalid code value",
            "8" => "Invalid date",
            "9" => "Invalid time",
            "10" => "Exclusion condition violated",
            _ => $"Error code {code}"
        };

    private void DetectDelimiters(string raw)
    {
        if (raw.Length >= 106) { _elementSeparator = raw[3]; _segmentTerminator = raw[105]; }
    }
}

/// <summary>
/// Parses 277CA (Claim Acknowledgment) transactions.
/// A payer sends a 277CA to acknowledge claim receipt and provide claim-level status.
/// Key linkage: REF*1K in 277CA = CLM01 in the original 837.
/// </summary>
public class Edi277CaParser
{
    private char _segmentTerminator = '~';
    private char _elementSeparator = '*';
    private char _subElementSeparator = ':';

    public (EdiTransaction transaction, List<Claim277CA> claimStatuses) Parse(string rawContent, int tradingPartnerId)
    {
        DetectDelimiters(rawContent);

        var segments = rawContent
            .Split(_segmentTerminator, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        var transaction = new EdiTransaction
        {
            TradingPartnerId = tradingPartnerId,
            TransactionType = "277CA",
            RawContent = rawContent,
            ReceivedAt = DateTime.UtcNow,
            Status = "Received"
        };

        var claimStatuses = new List<Claim277CA>();
        Claim277CA? current = null;
        string? currentHlCode = null;
        string? currentPatient = null;
        string? currentProvider = null;
        string? currentProviderId = null;

        foreach (var seg in segments)
        {
            var el = seg.Split(_elementSeparator);
            if (el.Length == 0) continue;
            var id = el[0].Trim();

            switch (id)
            {
                case "ISA":
                    transaction.ControlNumber = el.Length > 13 ? el[13].Trim() : "";
                    break;

                case "HL":
                    // HL*id*parent*levelCode*hasChild
                    currentHlCode = el.Length > 3 ? el[3].Trim() : "";
                    if (currentHlCode == "PT") // Patient/Claim level
                    {
                        if (current != null) claimStatuses.Add(current);
                        current = new Claim277CA
                        {
                            ReceivedAt = DateTime.UtcNow,
                            PatientName = currentPatient,
                            ProviderName = currentProvider,
                            ProviderId = currentProviderId
                        };
                    }
                    break;

                case "NM1":
                    // NM1*qualifier*type*last*first...
                    var nm1Qual = el.Length > 1 ? el[1].Trim() : "";
                    var lastName = el.Length > 3 ? el[3].Trim() : "";
                    var firstName = el.Length > 4 ? el[4].Trim() : "";
                    var npi = el.Length > 9 ? el[9].Trim() : "";
                    var fullName = string.IsNullOrEmpty(firstName) ? lastName : $"{lastName}, {firstName}";
                    switch (nm1Qual)
                    {
                        case "IL": // Subscriber
                        case "QC": // Patient
                            currentPatient = fullName;
                            if (current != null) current.PatientName = fullName;
                            break;
                        case "82": // Rendering Provider
                        case "1P": // Provider
                        case "85": // Billing Provider
                            currentProvider = fullName;
                            currentProviderId = npi;
                            if (current != null) { current.ProviderName = fullName; current.ProviderId = npi; }
                            break;
                    }
                    break;

                case "TRN":
                    // TRN*2*claimRefNumber  – trace for claim level
                    if (current != null && el.Length > 2 && (el.Length < 2 || el[1].Trim() == "2"))
                        if (string.IsNullOrEmpty(current.SubmitterClaimId))
                            current.SubmitterClaimId = el.Length > 2 ? el[2].Trim() : "";
                    break;

                case "REF":
                    if (current == null) break;
                    var refQual = el.Length > 1 ? el[1].Trim() : "";
                    var refVal  = el.Length > 2 ? el[2].Trim() : "";
                    switch (refQual)
                    {
                        case "1K": // Submitter claim ID – key linkage to 837 CLM01
                            current.SubmitterClaimId = refVal;
                            break;
                        case "D9": // Payer ICN
                            current.PayerClaimNumber = refVal;
                            break;
                    }
                    break;

                case "STC":
                    // STC*categoryCode:statusCode:entityCode*date*actionCode*totalCharge*paymentAmount
                    if (current == null) break;
                    if (el.Length > 1)
                    {
                        var stcParts = el[1].Split(_subElementSeparator);
                        current.StatusCategoryCode = stcParts.Length > 0 ? stcParts[0].Trim() : null;
                        current.StatusCode         = stcParts.Length > 1 ? stcParts[1].Trim() : null;
                        current.StatusDescription  = BuildStcDescription(current.StatusCategoryCode, current.StatusCode);
                    }
                    if (el.Length > 2 && DateTime.TryParseExact(el[2].Trim(), "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var stcDate))
                        current.StatusDate = stcDate;
                    current.ActionCode = el.Length > 3 ? el[3].Trim() : null;
                    if (el.Length > 4 && decimal.TryParse(el[4].Trim(), out var charge))
                        current.TotalClaimChargeAmount = charge;
                    if (el.Length > 5 && decimal.TryParse(el[5].Trim(), out var pmt))
                        current.PaymentAmount = pmt;
                    break;
            }
        }

        if (current != null) claimStatuses.Add(current);

        transaction.Status = claimStatuses.Count > 0 ? "Received" : "Received";
        return (transaction, claimStatuses);
    }

    private static string BuildStcDescription(string? category, string? status)
    {
        var cat = category switch
        {
            "A0" => "Acknowledgment / Not processed",
            "A1" => "Acknowledgment / Processed as primary",
            "A2" => "Acknowledgment / Processed as secondary",
            "A3" => "Acknowledgment / Processed as tertiary",
            "A4" => "Acknowledgment / Denied",
            "A6" => "Acknowledgment / Received, but not yet processed",
            "A7" => "Acknowledgment / Rejected",
            "A8" => "Acknowledgment / Accepted into adjudication system",
            "D0" => "Data Reporting Acknowledgment / Received, not yet processed",
            "F0" => "Finalized",
            "F1" => "Finalized / Payment",
            "F2" => "Finalized / Denial",
            "F3" => "Finalized / Revised",
            "F4" => "Finalized / Adjudication complete, no payment forthcoming",
            "P1" => "Pending / In process",
            "P2" => "Pending / Payer date",
            "P3" => "Pending / Patient date",
            "P4" => "Pending / Incomplete",
            "R0" => "Request for additional information",
            "R3" => "Returned as unprocessable claim",
            _ => $"Category {category}"
        };
        return status != null ? $"{cat} (Status: {status})" : cat;
    }

    private void DetectDelimiters(string raw)
    {
        if (raw.Length >= 106)
        {
            _elementSeparator   = raw[3];
            _segmentTerminator  = raw[105];
            _subElementSeparator = raw[104];
        }
    }
}

/// <summary>
/// Detects EDI transaction type from raw content.
/// </summary>
public static class EdiTypeDetector
{
    public static string Detect(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent)) return "UNKNOWN";

        // Check for TA1 segment
        if (rawContent.Contains("~TA1*") || rawContent.Contains("\nTA1*") ||
            System.Text.RegularExpressions.Regex.IsMatch(rawContent, @"TA1\*"))
            return "TA1";

        // Check ST segment for transaction set identifier
        var stMatch = System.Text.RegularExpressions.Regex.Match(rawContent, @"ST\*(\d{3})\*");
        if (stMatch.Success)
        {
            return stMatch.Groups[1].Value switch
            {
                "837" => Detect837SubType(rawContent),
                "999" => "999",
                "997" => "997",
                "835" => "835",
                "270" => "270",
                "271" => "271",
                "277" => Detect277SubType(rawContent),
                _ => $"X12_{stMatch.Groups[1].Value}"
            };
        }

        return "UNKNOWN";
    }

    private static string Detect837SubType(string raw)
    {
        if (raw.Contains("SV2*")) return "837I";
        if (raw.Contains("SV3*")) return "837D";
        return "837P";
    }

    private static string Detect277SubType(string raw)
    {
        // 277CA contains BHT*0085, plain 277 uses BHT*0010 or BHT*0019
        if (raw.Contains("BHT*0085") || raw.Contains("*CA*")) return "277CA";
        return "277";
    }
}
