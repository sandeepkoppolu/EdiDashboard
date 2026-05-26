/*
  Backfill999Acknowledgments.sql
  Purpose: Create missing Acknowledgments rows for existing 999 transactions.
  Target: SQL Server (EDIDashboard)
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    ;WITH Missing999 AS
    (
        SELECT t.Id,
               t.ControlNumber,
               t.ReceivedAt,
               t.RawContent
        FROM dbo.EdiTransactions t
        WHERE t.TransactionType = '999'
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.Acknowledgments a
              WHERE a.EdiTransactionId = t.Id
                AND a.AckType = '999'
          )
    ),
    Parsed AS
    (
        SELECT
            m.Id,
            m.ControlNumber,
            m.ReceivedAt,
            CASE
                WHEN pIk5.Pos > 0 THEN
                    SUBSTRING(m.RawContent, pIk5.Pos + 4,
                        CASE
                            WHEN pIk5Next.Pos > pIk5.Pos + 4 THEN pIk5Next.Pos - (pIk5.Pos + 4)
                            ELSE 1
                        END)
                WHEN pAk5.Pos > 0 THEN
                    SUBSTRING(m.RawContent, pAk5.Pos + 4,
                        CASE
                            WHEN pAk5Next.Pos > pAk5.Pos + 4 THEN pAk5Next.Pos - (pAk5.Pos + 4)
                            ELSE 1
                        END)
                ELSE ''
            END AS AckCode
        FROM Missing999 m
        CROSS APPLY (SELECT CHARINDEX('IK5*', m.RawContent) AS Pos) pIk5
        CROSS APPLY (SELECT CASE WHEN pIk5.Pos > 0 THEN CHARINDEX('*', m.RawContent, pIk5.Pos + 4) ELSE 0 END AS Pos) pIk5Next
        CROSS APPLY (SELECT CHARINDEX('AK5*', m.RawContent) AS Pos) pAk5
        CROSS APPLY (SELECT CASE WHEN pAk5.Pos > 0 THEN CHARINDEX('*', m.RawContent, pAk5.Pos + 4) ELSE 0 END AS Pos) pAk5Next
    )
    INSERT INTO dbo.Acknowledgments
    (
        EdiTransactionId,
        AckType,
        ControlNumber,
        AcknowledgmentCode,
        Description,
        ReceivedAt,
        FunctionalGroupControlNumber,
        TransactionSetControlNumber,
        ErrorCode,
        NoteCode
    )
    SELECT
        p.Id,
        '999',
        ISNULL(NULLIF(LTRIM(RTRIM(p.ControlNumber)), ''), ''),
        LEFT(ISNULL(LTRIM(RTRIM(p.AckCode)), ''), 1),
        CASE LEFT(ISNULL(LTRIM(RTRIM(p.AckCode)), ''), 1)
            WHEN 'A' THEN 'Transaction set accepted'
            WHEN 'E' THEN 'Transaction set accepted with errors'
            WHEN 'R' THEN 'Transaction set rejected'
            WHEN 'P' THEN 'Transaction set partially accepted'
            ELSE '999 acknowledgment backfilled from transaction row'
        END,
        p.ReceivedAt,
        NULL,
        NULL,
        NULL,
        NULL
    FROM Parsed p;

    COMMIT TRANSACTION;

    SELECT AckType, COUNT(*) AS CountByType
    FROM dbo.Acknowledgments
    GROUP BY AckType
    ORDER BY AckType;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSev INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();

    RAISERROR('Backfill failed: %s', @ErrSev, @ErrState, @ErrMsg);
END CATCH;
