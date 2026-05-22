using EdiProcessor.Core.Parsers;

namespace EdiProcessor.Core.Tests.Parsers;

public class Edi837ParserTests
{
    [Fact]
    public void Parse_ProfessionalClaim_ParsesCoreFieldsAndServiceLine()
    {
        var parser = new Edi837Parser();
        var raw = "ISA*00*          *00*          *ZZ*SENDERID       *ZZ*RECEIVERID     *240101*1230*^*00501*000000905*0*T*:~" +
                  "ST*837*0001~" +
                  "NM1*IL*1*DOE*JANE****MI*SUB1~" +
                  "NM1*82*1*PROV*JOHN****XX*12345~" +
                  "NM1*PR*2*PAYERORG*****PI*PAY1~" +
                  "CLM*CLM123*150.50***11:B:1*Y*A*Y*I~" +
                  "DTP*472*D8*20240115~" +
                  "LX*1~" +
                  "SV1*HC:99213:25*100.00*UN*2***1~";

        var (transaction, claims) = parser.Parse(raw, 7);

        Assert.Equal(7, transaction.TradingPartnerId);
        Assert.Equal("000000905", transaction.ControlNumber);
        Assert.Equal("837P", transaction.TransactionType);
        Assert.Single(claims);

        var claim = claims[0];
        Assert.Equal("CLM123", claim.ClaimNumber);
        Assert.Equal(150.50m, claim.TotalAmount);
        Assert.Equal("Professional", claim.ClaimType);
        Assert.Equal("JANE DOE", claim.PatientName);
        Assert.Equal("JOHN PROV", claim.ProviderName);
        Assert.Equal("12345", claim.ProviderId);
        Assert.Equal("PAY1", claim.PayerId);
        Assert.Equal("PAYERORG", claim.PayerName);
        Assert.Equal(new DateTime(2024, 1, 15), claim.ServiceDateFrom);

        Assert.Single(claim.ServiceLines);
        var line = claim.ServiceLines[0];
        Assert.Equal("99213", line.ProcedureCode);
        Assert.Equal("25", line.Modifier);
        Assert.Equal(100.00m, line.ChargedAmount);
        Assert.Equal(2, line.Units);
        Assert.Equal("1", line.DiagnosisPointers);
        Assert.Equal(new DateTime(2024, 1, 15), line.ServiceDate);
    }

    [Fact]
    public void Parse_InstitutionalClaim_UsesSv2AndDateRangeAndDefaults()
    {
        var parser = new Edi837Parser();
        var raw = "ISA*00*          *00*          *ZZ*SENDERID       *ZZ*RECEIVERID     *240101*1230*^*00501*000000906*0*T*:~" +
                  "ST*837*0002~" +
                  "NM1*QC*1*PATIENT*TEST~" +
                  "CLM*CLM456*ABC***11:B:1*Y*A*Y*I~" +
                  "DTP*472*RD8*20240201-20240203~" +
                  "SV2*0300*HC:REV001*XYZ*UN*X~";

        var (transaction, claims) = parser.Parse(raw, 9);

        Assert.Equal("837I", transaction.TransactionType);
        Assert.Single(claims);

        var claim = claims[0];
        Assert.Equal("Institutional", claim.ClaimType);
        Assert.Equal(0m, claim.TotalAmount);
        Assert.Equal(new DateTime(2024, 2, 1), claim.ServiceDateFrom);
        Assert.Equal(new DateTime(2024, 2, 3), claim.ServiceDateTo);
        Assert.Single(claim.ServiceLines);
        Assert.Equal("REV001", claim.ServiceLines[0].ProcedureCode);
        Assert.Equal(0m, claim.ServiceLines[0].ChargedAmount);
        Assert.Equal(1, claim.ServiceLines[0].Units);
    }

    [Fact]
    public void Parse_DentalClaimAndMultipleClaims_AddsAllClaims()
    {
        var parser = new Edi837Parser();
        var raw = "ISA*00*          *00*          *ZZ*SENDERID       *ZZ*RECEIVERID     *240101*1230*^*00501*000000907*0*T*:~" +
                  "ST*837*0003~" +
                  "NM1*82*1*DOC*DENT****XX*D123~" +
                  "CLM*CLM1*10~" +
                  "SV3*AD:D1110*10*UN*1~" +
                  "CLM*CLM2*20~";

        var (transaction, claims) = parser.Parse(raw, 3);

        Assert.Equal("837D", transaction.TransactionType);
        Assert.Equal(2, claims.Count);
        Assert.All(claims, c => Assert.Equal("Dental", c.ClaimType));
        Assert.Equal("CLM1", claims[0].ClaimNumber);
        Assert.Equal("CLM2", claims[1].ClaimNumber);
        Assert.Equal("DENT DOC", claims[0].ProviderName);
        Assert.Equal("D123", claims[1].ProviderId);
    }
}
