/************************************************************
* This script Drops All Snapshots of the existing database.
*------------------------------------------------------------
*    Date    Alais          Description
* ---------- -------------- ---------------------------------
* 2013/05/28 EdWhite        Created
************************************************************/
-- Set Environment variables to allow for index views
SET ARITHABORT              ON
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_PADDING            ON
SET ANSI_WARNINGS           ON
SET NUMERIC_ROUNDABORT      OFF
SET NOCOUNT                 ON

-- Local variable declaration
DECLARE @DatabaseName            sysname            = db_name()              -- Name of the database this routine is running in.
DECLARE @DropSnapshot            nvarchar(max)      = ''                     -- List of Drop Snapshot commands
DECLARE @nl                      char(2)            = char(13) + char(10)    -- New Line for SQL Commands

-- Main Try/Catch starts
BEGIN TRY

  /**************************************
  * Check if Snapshot does exists, all existing snapshots need to be dropped.
  **************************************/
  -- Build Drop Snapshots commands
  SELECT @DropSnapshot += 'EXECUTE ( ''DROP DATABASE [' + sd.Name + ']'' )' + @nl
    FROM sys.databases sd
    WHERE sd.source_database_id = db_id()
   
  -- 
  IF @DropSnapshot <> ''  -- There are Snapshots
  BEGIN
    /*************************************
    * Drop existing Snapshot(s)
    *************************************/
    EXECUTE ( @DropSnapshot )

  END -- IF @SnapshotName IS NOT NULL

/**************************************************
* Main Catch Section
**************************************************/
END TRY
BEGIN CATCH
  THROW;
END CATCH
