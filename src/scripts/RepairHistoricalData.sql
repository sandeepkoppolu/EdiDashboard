/*
  RepairHistoricalData.sql
  Purpose: Repair existing data created before recent fixes.
  Target: SQL Server (EDIDashboard)

  Repairs included:
  1) Re-map TA1/999/277CA EdiTransactions to ISA06 sender trading partner.
  2) Backfill FileProcessingLogs.SubmissionDate from FileName prefix (MMddyyyy).
  3) Backfill missing FileProcessingLogs rows from EdiTransactions (for historical uploads
     before upload logging was added), including SubmissionDate and ClaimsProcessed.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT 'Starting historical data repair...';

    /* ---------------------------------------------------------------
       1) Re-map acknowledgments to ISA06 sender partner
       --------------------------------------------------------------- */
    ;WITH AckTx AS
    (
        SELECT t.Id, t.RawContent
        FROM dbo.EdiTransactions t
        WHERE t.TransactionType IN ('TA1', '999', '277CA')
          AND t.RawContent LIKE 'ISA%'
    ),
    Parsed AS
    (
        SELECT
            a.Id,
            LTRIM(RTRIM(SUBSTRING(a.RawContent, p6.Pos + 1, p7.Pos - p6.Pos - 1))) AS Isa06
        FROM AckTx a
        CROSS APPLY (SELECT CHARINDEX('*', a.RawContent, 1) AS Pos) p1
        CROSS APPLY (SELECT CHARINDEX('*', a.RawContent, p1.Pos + 1) AS Pos) p2
        CROSS APPLY (SELECT CHARINDEX('*', a.RawContent, p2.Pos + 1) AS Pos) p3
        CROSS APPLY (SELECT CHARINDEX('*', a.RawContent, p3.Pos + 1) AS Pos) p4
        CROSS APPLY (SELECT CHARINDEX('*', a.RawContent, p4.Pos + 1) AS Pos) p5
        CROSS APPLY (SELECT CHARINDEX('*', a.RawContent, p5.Pos + 1) AS Pos) p6
        CROSS APPLY (SELECT CHARINDEX('*', a.RawContent, p6.Pos + 1) AS Pos) p7
        WHERE p6.Pos > 0 AND p7.Pos > p6.Pos
    )
    UPDATE t
    SET t.TradingPartnerId = tp.Id
    FROM dbo.EdiTransactions t
    INNER JOIN Parsed p ON p.Id = t.Id
    INNER JOIN dbo.TradingPartners tp ON tp.InterchangeId = p.Isa06
    WHERE p.Isa06 <> ''
      AND t.TradingPartnerId <> tp.Id;

    PRINT 'Acknowledgment partner remap complete.';

    /* ---------------------------------------------------------------
       2) Backfill SubmissionDate from FileName (MMddyyyy prefix)
       --------------------------------------------------------------- */
    UPDATE dbo.FileProcessingLogs
    SET SubmissionDate = TRY_CONVERT(date, STUFF(STUFF(LEFT(FileName, 8), 3, 0, '/'), 6, 0, '/'), 101)
    WHERE SubmissionDate IS NULL
      AND LEN(FileName) >= 8;

    PRINT 'SubmissionDate backfill complete.';

    /* ---------------------------------------------------------------
       3) Backfill missing FileProcessingLogs from EdiTransactions
       --------------------------------------------------------------- */
    ;WITH TxClaimCounts AS
    (
        SELECT
            t.Id,
            t.FileName,
            t.TransactionType,
            t.ControlNumber,
            t.TradingPartnerId,
            t.ReceivedAt,
            t.Status,
            t.ErrorDescription,
            COUNT(c.Id) AS ClaimsProcessed
        FROM dbo.EdiTransactions t
        LEFT JOIN dbo.Claims c ON c.EdiTransactionId = t.Id
        WHERE t.FileName IS NOT NULL
          AND LTRIM(RTRIM(t.FileName)) <> ''
        GROUP BY
            t.Id,
            t.FileName,
            t.TransactionType,
            t.ControlNumber,
            t.TradingPartnerId,
            t.ReceivedAt,
            t.Status,
            t.ErrorDescription
    )
    INSERT INTO dbo.FileProcessingLogs
    (
        FileName,
        SubmissionDate,
        OriginalPath,
        FinalPath,
        FileSizeBytes,
        DetectedType,
        TradingPartnerId,
        Status,
        ErrorMessage,
        ControlNumber,
        ClaimsProcessed,
        PickedUpAt,
        CompletedAt,
        ProcessingDuration,
        Source
    )
    SELECT
        tcc.FileName,
        TRY_CONVERT(date, STUFF(STUFF(LEFT(tcc.FileName, 8), 3, 0, '/'), 6, 0, '/'), 101) AS SubmissionDate,
        tcc.FileName AS OriginalPath,
        tcc.FileName AS FinalPath,
        0 AS FileSizeBytes,
        tcc.TransactionType AS DetectedType,
        tcc.TradingPartnerId,
        CASE
            WHEN tcc.Status = 'Rejected' THEN 'Failed'
            WHEN tcc.Status = 'Accepted' THEN 'Success'
            WHEN tcc.Status = 'Received' THEN 'Success'
            ELSE 'Success'
        END AS Status,
        CASE WHEN tcc.Status = 'Rejected' THEN tcc.ErrorDescription ELSE NULL END AS ErrorMessage,
        tcc.ControlNumber,
        tcc.ClaimsProcessed,
        tcc.ReceivedAt AS PickedUpAt,
        tcc.ReceivedAt AS CompletedAt,
        CAST('00:00:00' AS time) AS ProcessingDuration,
        'Upload' AS Source
    FROM TxClaimCounts tcc
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.FileProcessingLogs l
        WHERE l.FileName = tcc.FileName
          AND ISNULL(l.ControlNumber, '') = ISNULL(tcc.ControlNumber, '')
          AND ISNULL(l.DetectedType, '') = ISNULL(tcc.TransactionType, '')
          AND l.PickedUpAt = tcc.ReceivedAt
    );

    PRINT 'Missing FileProcessingLogs backfill complete.';

    COMMIT TRANSACTION;
    PRINT 'Historical data repair complete.';

    /* Quick verification result sets */
    SELECT TOP 20 Id, TransactionType, TradingPartnerId, ControlNumber, LEFT(RawContent, 120) AS RawPreview
    FROM dbo.EdiTransactions
    WHERE TransactionType IN ('TA1', '999', '277CA')
    ORDER BY Id DESC;

    SELECT TOP 20 Id, FileName, SubmissionDate, Status, ClaimsProcessed, Source
    FROM dbo.FileProcessingLogs
    ORDER BY Id DESC;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSev INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();

    RAISERROR('Repair failed: %s', @ErrSev, @ErrState, @ErrMsg);
END CATCH;
