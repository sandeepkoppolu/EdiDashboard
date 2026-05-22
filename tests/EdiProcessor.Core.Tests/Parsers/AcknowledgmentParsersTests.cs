using EdiProcessor.Core.Parsers;

namespace EdiProcessor.Core.Tests.Parsers;

public class Ta1ParserTests
{
    [Fact]
    public void Parse_AcceptedTa1_SetsAcceptedStatusAndDescription()
    {
        var parser = new Ta1Parser();
        var raw = "ISA*00*          *00*          *ZZ*SENDERID       *ZZ*RECEIVERID     *240101*1230*^*00501*000000905*0*T*:~TA1*000000905*030101*1234*A*000~";

        var (transaction, ack) = parser.Parse(raw, 1);

        Assert.Equal("TA1", transaction.TransactionType);
        Assert.Equal("Accepted", transaction.Status);
        Assert.Equal("000000905", transaction.ControlNumber);
        Assert.Equal("A", ack.AcknowledgmentCode);
        Assert.Equal("Interchange accepted", ack.Description);
    }

    [Fact]
    public void Parse_RejectedTa1_SetsRejectedStatusAndNoteDescription()
    {
        var parser = new Ta1Parser();
        var raw = "TA1*000000777*030101*1234*R*025~";

        var (transaction, ack) = parser.Parse(raw, 1);

        Assert.Equal("Rejected", transaction.Status);
        Assert.Equal("025", ack.NoteCode);
        Assert.Contains("Duplicate interchange control number", ack.Description);
    }
}

public class Edi999ParserTests
{
    [Fact]
    public void Parse_Rejected999_BuildsErrorDescription()
    {
        var parser = new Edi999Parser();
        var raw = "ISA*00*          *00*          *ZZ*SENDERID       *ZZ*RECEIVERID     *240101*1230*^*00501*000000908*0*T*:~" +
                  "ST*999*0001~" +
                  "AK1*HC*000000001~" +
                  "AK2*837*0001~" +
                  "IK3*CLM*2*8~" +
                  "IK4*1*1250*1*7~" +
                  "AK5*R*5~" +
                  "AK9*R*1*1*0~";

        var (transaction, acks) = parser.Parse(raw, 2);

        Assert.Equal("Rejected", transaction.Status);
        Assert.Single(acks);
        var ack = acks[0];
        Assert.Equal("000000001", ack.FunctionalGroupControlNumber);
        Assert.Equal("0001", ack.TransactionSetControlNumber);
        Assert.Equal("R", ack.AcknowledgmentCode);
        Assert.Equal("5", ack.ErrorCode);
        Assert.Contains("Segment error", ack.Description);
        Assert.Contains("Invalid code value", ack.Description);
    }

    [Fact]
    public void Parse_Accepted999_ProducesAcceptedDescription()
    {
        var parser = new Edi999Parser();
        var raw = "AK1*HC*777~AK2*837*0009~AK5*A~AK9*A*1*1*1~";

        var (transaction, acks) = parser.Parse(raw, 2);

        Assert.Equal("Accepted", transaction.Status);
        Assert.Single(acks);
        Assert.Equal("Transaction set accepted", acks[0].Description);
    }
}

public class Edi277CaParserTests
{
    [Fact]
    public void Parse_277Ca_ParsesClaimLevelStatusFields()
    {
        var parser = new Edi277CaParser();
        var raw = "ISA*00*          *00*          *ZZ*SENDERID       *ZZ*RECEIVERID     *240101*1230*^*00501*000000909*0*T*:~" +
                  "ST*277*0001~" +
                  "HL*1**20*1~" +
                  "NM1*85*2*BILLPROV*****XX*1234567890~" +
                  "HL*2*1*PT*0~" +
                  "NM1*QC*1*DOE*JANE~" +
                  "TRN*2*TRN001~" +
                  "REF*1K*SUBM123~" +
                  "REF*D9*PAY123~" +
                  "STC*A1:19:PR*20240201*WQ*250.00*100.00~";

        var (transaction, statuses) = parser.Parse(raw, 11);

        Assert.Equal("277CA", transaction.TransactionType);
        Assert.Equal("000000909", transaction.ControlNumber);
        Assert.Single(statuses);

        var status = statuses[0];
        Assert.Equal("SUBM123", status.SubmitterClaimId);
        Assert.Equal("PAY123", status.PayerClaimNumber);
        Assert.Equal("A1", status.StatusCategoryCode);
        Assert.Equal("19", status.StatusCode);
        Assert.Contains("Processed as primary", status.StatusDescription);
        Assert.Equal(new DateTime(2024, 2, 1), status.StatusDate);
        Assert.Equal("WQ", status.ActionCode);
        Assert.Equal(250.00m, status.TotalClaimChargeAmount);
        Assert.Equal(100.00m, status.PaymentAmount);
        Assert.Equal("DOE, JANE", status.PatientName);
        Assert.Equal("BILLPROV", status.ProviderName);
    }

    [Fact]
    public void Parse_277Ca_UnknownCategoryAndMultiplePtLoops_AreHandled()
    {
        var parser = new Edi277CaParser();
        var raw = "HL*1**PT*1~NM1*IL*1*SMITH*JOHN~TRN*1*IGNORED~STC*ZZ:42:PR*BADDATE~" +
                  "HL*2*1*PT*0~REF*1K*CLM2~STC*P1:20:PR*20240501~";

        var (_, statuses) = parser.Parse(raw, 5);

        Assert.Equal(2, statuses.Count);
        Assert.Contains("Category ZZ", statuses[0].StatusDescription);
        Assert.Null(statuses[0].StatusDate);
        Assert.Equal("CLM2", statuses[1].SubmitterClaimId);
        Assert.Equal("P1", statuses[1].StatusCategoryCode);
    }
}

public class EdiTypeDetectorTests
{
    [Theory]
    [InlineData("", "UNKNOWN")]
    [InlineData("TA1*000*030101*1234*A*000~", "TA1")]
    [InlineData("ST*837*0001~SV1*HC:99213~", "837P")]
    [InlineData("ST*837*0001~SV2*0300*HC:11111~", "837I")]
    [InlineData("ST*837*0001~SV3*AD:D1110~", "837D")]
    [InlineData("ST*999*0001~", "999")]
    [InlineData("ST*997*0001~", "997")]
    [InlineData("ST*835*0001~", "835")]
    [InlineData("ST*270*0001~", "270")]
    [InlineData("ST*271*0001~", "271")]
    [InlineData("ST*277*0001~BHT*0085*08*ABCD~", "277CA")]
    [InlineData("ST*277*0001~BHT*0010*08*ABCD~", "277")]
    [InlineData("ST*123*0001~", "X12_123")]
    public void Detect_ReturnsExpectedType(string raw, string expected)
    {
        var actual = EdiTypeDetector.Detect(raw);
        Assert.Equal(expected, actual);
    }
}
