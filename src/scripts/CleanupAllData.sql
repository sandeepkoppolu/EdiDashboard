/*
  CleanupAllData.sql
  Purpose: Remove all application data from EDIDashboard database while keeping schema and migrations.
  Target: SQL Server

  Notes:
  - This script deletes data in foreign-key-safe order.
  - It keeps __EFMigrationsHistory intact.
  - It reseeds identity columns back to 0 so next insert starts at 1.
    - It clears FileProcessingLogs entries used by the watcher, uploads, and Daily Volume widget.
    - It is safe against the current schema, including Claims277CA and FileProcessingLogs.SubmissionDate.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT 'Starting EDIDashboard data cleanup...';

    /* Child/dependent tables first */
    IF OBJECT_ID(N'[ServiceLines]', N'U') IS NOT NULL DELETE FROM [ServiceLines];
    IF OBJECT_ID(N'[Claims277CA]', N'U') IS NOT NULL DELETE FROM [Claims277CA];
    IF OBJECT_ID(N'[Claims]', N'U') IS NOT NULL DELETE FROM [Claims];
    IF OBJECT_ID(N'[Acknowledgments]', N'U') IS NOT NULL DELETE FROM [Acknowledgments];
    IF OBJECT_ID(N'[FileProcessingLogs]', N'U') IS NOT NULL DELETE FROM [FileProcessingLogs];

    /* Parent tables */
    IF OBJECT_ID(N'[EdiTransactions]', N'U') IS NOT NULL DELETE FROM [EdiTransactions];
    IF OBJECT_ID(N'[TradingPartners]', N'U') IS NOT NULL DELETE FROM [TradingPartners];

    /* Reseed identity values */
    IF OBJECT_ID(N'[ServiceLines]', N'U') IS NOT NULL DBCC CHECKIDENT ('ServiceLines', RESEED, 0);
    IF OBJECT_ID(N'[Claims277CA]', N'U') IS NOT NULL DBCC CHECKIDENT ('Claims277CA', RESEED, 0);
    IF OBJECT_ID(N'[Claims]', N'U') IS NOT NULL DBCC CHECKIDENT ('Claims', RESEED, 0);
    IF OBJECT_ID(N'[Acknowledgments]', N'U') IS NOT NULL DBCC CHECKIDENT ('Acknowledgments', RESEED, 0);
    IF OBJECT_ID(N'[FileProcessingLogs]', N'U') IS NOT NULL DBCC CHECKIDENT ('FileProcessingLogs', RESEED, 0);
    IF OBJECT_ID(N'[EdiTransactions]', N'U') IS NOT NULL DBCC CHECKIDENT ('EdiTransactions', RESEED, 0);
    IF OBJECT_ID(N'[TradingPartners]', N'U') IS NOT NULL DBCC CHECKIDENT ('TradingPartners', RESEED, 0);

    COMMIT TRANSACTION;

    PRINT 'Cleanup complete. All application data deleted.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSev INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();

    RAISERROR('Cleanup failed: %s', @ErrSev, @ErrState, @ErrMsg);
END CATCH;
