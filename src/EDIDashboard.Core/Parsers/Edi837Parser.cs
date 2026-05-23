using EDIDashboard.Core.Models;

namespace EDIDashboard.Core.Parsers;

/// <summary>
/// Parses EDI 837 (Professional, Institutional, Dental) files into domain objects.
/// Handles standard X12 segment/element delimiters discovered from the ISA segment.
/// </summary>
public class Edi837Parser
{
    private char _segmentTerminator = '~';
    private char _elementSeparator = '*';
    private char _componentSeparator = ':';

    public (EdiTransaction transaction, List<Claim837> claims) Parse(string rawContent, int tradingPartnerId)
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
            RawContent = rawContent,
            ReceivedAt = DateTime.UtcNow,
            Status = "Received"
        };

        var claims = new List<Claim837>();
        Claim837? currentClaim = null;
        ServiceLine? currentLine = null;

        string claimType = "Professional";
        string currentPatientName = "";
        string currentProviderName = "";
        string currentProviderId = "";
        string currentPayerId = "";
        string currentPayerName = "";
        string currentLoopId = "";
        bool inPatientLoop = false;
        bool inSubscriberLoop = false;
        var diagnosisCodes = new List<string>();

        foreach (var seg in segments)
        {
            var el = seg.Split(_elementSeparator);
            if (el.Length == 0) continue;
            var id = el[0].Trim();

            switch (id)
            {
                case "ISA":
                    transaction.ControlNumber = el.Length > 13 ? el[13].Trim() : "UNKNOWN";
                    break;

                case "ST":
                    // ST*837*0001
                    var stCode = el.Length > 1 ? el[1] : "";
                    claimType = stCode switch
                    {
                        "837" => DetermineClaimType(segments),
                        _ => "Professional"
                    };
                    transaction.TransactionType = $"837{claimType[0]}"; // 837P, 837I, 837D
                    break;

                case "BPR": break; // financial info, skip for now

                case "NM1":
                    var nm1Qualifier = el.Length > 1 ? el[1] : "";
                    var lastName = el.Length > 3 ? el[3] : "";
                    var firstName = el.Length > 4 ? el[4] : "";
                    var idCode = el.Length > 9 ? el[9] : "";

                    switch (nm1Qualifier)
                    {
                        case "QC": // Patient
                            currentPatientName = $"{firstName} {lastName}".Trim();
                            if (currentClaim != null) currentClaim.PatientName = currentPatientName;
                            break;
                        case "IL": // Subscriber (when patient = subscriber)
                            if (currentClaim != null && string.IsNullOrEmpty(currentClaim.PatientName))
                                currentClaim.PatientName = $"{firstName} {lastName}".Trim();
                            break;
                        case "82": // Rendering Provider
                            currentProviderName = $"{firstName} {lastName}".Trim();
                            currentProviderId = idCode;
                            if (currentClaim != null)
                            {
                                currentClaim.ProviderName = currentProviderName;
                                currentClaim.ProviderId = currentProviderId;
                            }
                            break;
                        case "85": // Billing Provider
                            if (string.IsNullOrEmpty(currentProviderName))
                            {
                                currentProviderName = $"{firstName} {lastName}".Trim();
                                currentProviderId = idCode;
                            }
                            break;
                        case "PR": // Payer
                            currentPayerName = lastName; // payer orgs go in last name
                            currentPayerId = idCode;
                            if (currentClaim != null)
                            {
                                currentClaim.PayerName = currentPayerName;
                                currentClaim.PayerId = currentPayerId;
                            }
                            break;
                    }
                    break;

                case "CLM":
                    // Save previous claim
                    if (currentClaim != null) claims.Add(currentClaim);

                    currentClaim = new Claim837
                    {
                        ClaimNumber = el.Length > 1 ? el[1] : "",
                        TotalAmount = el.Length > 2 ? ParseDecimal(el[2]) : 0,
                        ClaimType = claimType,
                        Status = "Received",
                        CreatedAt = DateTime.UtcNow,
                        PatientName = currentPatientName,
                        ProviderName = currentProviderName,
                        ProviderId = currentProviderId,
                        PayerId = currentPayerId,
                        PayerName = currentPayerName
                    };
                    diagnosisCodes.Clear();
                    break;

                case "HI": // Diagnosis codes
                    for (int i = 1; i < el.Length; i++)
                    {
                        var parts = el[i].Split(_componentSeparator);
                        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                            diagnosisCodes.Add(parts[1]);
                    }
                    break;

                case "DTP":
                    // DTP*472*D8*20240101 - Service date
                    if (el.Length > 3 && el[1] == "472" && currentClaim != null)
                    {
                        if (el[2] == "D8")
                            currentClaim.ServiceDateFrom = ParseDate8(el[3]);
                        else if (el[2] == "RD8")
                        {
                            var dates = el[3].Split('-');
                            currentClaim.ServiceDateFrom = ParseDate8(dates[0]);
                            if (dates.Length > 1) currentClaim.ServiceDateTo = ParseDate8(dates[1]);
                        }
                    }
                    break;

                case "LX": // Line counter - new service line
                    currentLine = null;
                    break;

                case "SV1": // Professional service line
                    if (currentClaim != null)
                    {
                        var procParts = el.Length > 1 ? el[1].Split(_componentSeparator) : Array.Empty<string>();
                        currentLine = new ServiceLine
                        {
                            ProcedureCode = procParts.Length > 1 ? procParts[1] : "",
                            Modifier = procParts.Length > 2 ? procParts[2] : null,
                            ChargedAmount = el.Length > 2 ? ParseDecimal(el[2]) : 0,
                            Units = el.Length > 4 ? ParseInt(el[4]) : 1,
                            DiagnosisPointers = el.Length > 7 ? el[7] : "",
                            ServiceDate = currentClaim.ServiceDateFrom
                        };
                        currentClaim.ServiceLines.Add(currentLine);
                    }
                    break;

                case "SV2": // Institutional service line
                    if (currentClaim != null)
                    {
                        currentLine = new ServiceLine
                        {
                            ProcedureCode = el.Length > 2 ? el[2].Split(_componentSeparator).ElementAtOrDefault(1) ?? "" : "",
                            ChargedAmount = el.Length > 3 ? ParseDecimal(el[3]) : 0,
                            Units = el.Length > 5 ? ParseInt(el[5]) : 1,
                            ServiceDate = currentClaim.ServiceDateFrom
                        };
                        currentClaim.ServiceLines.Add(currentLine);
                    }
                    break;
            }
        }

        if (currentClaim != null) claims.Add(currentClaim);

        return (transaction, claims);
    }

    private string DetermineClaimType(List<string> segments)
    {
        // Look for GS segment functional identifier (HC = health care)
        // or check SV1 vs SV2 vs SV3
        bool hasSV1 = segments.Any(s => s.StartsWith("SV1*"));
        bool hasSV2 = segments.Any(s => s.StartsWith("SV2*"));
        bool hasSV3 = segments.Any(s => s.StartsWith("SV3*"));

        if (hasSV2) return "Institutional";
        if (hasSV3) return "Dental";
        return "Professional";
    }

    private void DetectDelimiters(string raw)
    {
        if (raw.Length >= 106)
        {
            _elementSeparator = raw[3];
            _componentSeparator = raw[104];
            _segmentTerminator = raw[105];
        }
    }

    private decimal ParseDecimal(string s) =>
        decimal.TryParse(s.Trim(), out var v) ? v : 0;

    private int ParseInt(string s) =>
        int.TryParse(s.Trim(), out var v) ? v : 1;

    private DateTime ParseDate8(string s) =>
        DateTime.TryParseExact(s.Trim(), "yyyyMMdd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d : DateTime.UtcNow.Date;
}
