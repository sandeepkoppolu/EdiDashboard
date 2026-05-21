/*
  CleanupAllData.sql
  Purpose: Remove all application data from EdiProcessor database while keeping schema and migrations.
  Target: SQL Server

  Notes:
  - This script deletes data in foreign-key-safe order.
  - It keeps __EFMigrationsHistory intact.
  - It reseeds identity columns back to 0 so next insert starts at 1.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT 'Starting EdiProcessor data cleanup...';

    /* Child/dependent tables first */
    DELETE FROM [ServiceLines];
    DELETE FROM [Claims277CA];
    DELETE FROM [Claims];
    DELETE FROM [Acknowledgments];
    DELETE FROM [FileProcessingLogs];

    /* Parent tables */
    DELETE FROM [EdiTransactions];
    DELETE FROM [TradingPartners];

    /* Reseed identity values */
    DBCC CHECKIDENT ('ServiceLines', RESEED, 0);
    DBCC CHECKIDENT ('Claims277CA', RESEED, 0);
    DBCC CHECKIDENT ('Claims', RESEED, 0);
    DBCC CHECKIDENT ('Acknowledgments', RESEED, 0);
    DBCC CHECKIDENT ('FileProcessingLogs', RESEED, 0);
    DBCC CHECKIDENT ('EdiTransactions', RESEED, 0);
    DBCC CHECKIDENT ('TradingPartners', RESEED, 0);

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
