/************************************************************
* This script Creates a Snapshot of the existing database with the extension of _xpfss.
*------------------------------------------------------------
*    Date    Alais          Description
* ---------- -------------- ---------------------------------
* 2014/09/19 EdWhite        Created
************************************************************/
-- Set Environment variables to allow for index views
SET ARITHABORT              ON
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_PADDING            ON
SET ANSI_WARNINGS           ON
SET NUMERIC_ROUNDABORT      OFF
SET NOCOUNT                 ON

-- Local variable declaration
DECLARE @CurrentDate             datetime           = GETDATE()              -- Starting date of this procedure
DECLARE @DatabaseName            sysname            = db_name()              -- Name of the database this routine is running in.
DECLARE @DropSnapshot            nvarchar(max)      = ''                     -- List of Drop Snapshot commands
DECLARE @Error                   integer            = 0                      -- Local variable to capture the error code.
DECLARE @ErrorLevel              integer            = 16                     -- Error Level
DECLARE @ErrorMessage            nvarchar(max)                               -- Error Message
DECLARE @inDebug                 integer           = 0
DECLARE @nl                      char(2)            = char(13) + char(10)    -- New Line for SQL Commands
DECLARE @ProcName                sysname            = OBJECT_NAME(@@procid)  -- Name of this procedure
DECLARE @ProcStep                varchar(35)                                 -- Current Processing Step
DECLARE @RowCnt                  integer                                     -- to capture number of rows affected.
DECLARE @SnapShotName            sysname                                     -- Name of Snapshot
DECLARE @SQLCmd                  nvarchar(max)      = ''                     -- SQL Command
DECLARE @rtncd                   integer                                     -- Return code.

-- Main Try/Catch starts
BEGIN TRY

  -- Set local variables
  SELECT @inDebug  = isnull( @inDebug, 0 )

  /**************************************
  * Check if any Snapshot does exists, all existing snapshots need to be dropped.
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

  /**************************************
  * Create the Snapshot Command
  **************************************/
  SET @SqlCmd = ''
  SET @SnapShotName = DB_NAME() + '_xpfss'

  -- Add Files
  SELECT @SqlCmd += CASE WHEN row_number() OVER ( ORDER BY mf.name ) = 1 THEN 'ON' ELSE ',' END
                  + ' ( NAME=[' + mf.name + '], FILENAME='''
                  + REPLACE( REPLACE ( mf.Physical_name, '.ndf', '_xpfss.ndf' ), '.mdf', '_xpfss.mdf' )
                  + ''' )'
    FROM sys.master_files mf
   WHERE mf.database_id = db_id()
     AND type_desc     <> 'Log'
   ORDER BY mf.name

  -- Add Rest of the Command
  SET @SqlCmd = 'CREATE DATABASE ' + @SnapShotName + @nl
              + @SqlCmd + @nl
              + 'AS SNAPSHOT OF [' + @DatabaseName + ']'

  IF @inDebug & 1 > 0
  BEGIN
    PRINT CONVERT( CHAR(12), GETDATE(), 14 ) + '> @SqlCmd = ' + isnull( @SqlCmd, '<NULL>' )
  END

  /**************************************
  * Execute the Command
  **************************************/
  EXECUTE( @SqlCmd )

/**************************************************
* Main Catch Section
**************************************************/
END TRY
BEGIN CATCH
  THROW;
END CATCH
