/************************************************************
* This script generates the Restore DB command. The output needs to be run in master. 
*------------------------------------------------------------
*    Date    Alias          Description
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
DECLARE @DbBackupCreateDate      datetime                                    -- Database Backup date from Backup Label
DECLARE @DropSnapshot            nvarchar(max)      = ''                     -- List of Drop Snapshot commands
DECLARE @Error                   integer            = 0                      -- Local variable to capture the error code.
DECLARE @ErrorLevel              integer            = 16                     -- Error Level
DECLARE @ErrorMessage            nvarchar(max)                               -- Error Message
DECLARE @MoveCmd                 nvarchar(max)      = ''                     -- Database Move commands
DECLARE @nl                      char(2)            = char(13) + char(10)    -- New Line for SQL Commands
DECLARE @ProcName                sysname            = OBJECT_NAME(@@procid)  -- Name of this procedure
DECLARE @ProcStep                varchar(35)                                 -- Current Processing Step
DECLARE @RowCnt                  integer                                     -- to capture number of rows affected.
DECLARE @rtncd                   integer                                     -- Return code.
DECLARE @SnapShotCreateDate      datetime                                    -- Date the database Snapshot was created
DECLARE @SnapShotName            sysname                                     -- Name of Snapshot
--DECLARE @SQLCmd                  nvarchar(max)      = ''                     -- SQL Command
DECLARE @UseSnapShot             bit                = 0                      -- Is Snapshot being used, defualt to No

-- Main Try/Catch starts
BEGIN TRY

  /***********************************************
  * Get name of last Snapshot created.
  ***********************************************/
  SELECT @SnapShotName       = sd2.Name
    FROM ( SELECT MAX( sd.Create_Date )   AS [Create_Date]
             FROM sys.databases sd
            WHERE sd.source_database_id = db_id()
         ) as sd1
    JOIN sys.databases sd2
      ON sd2.source_database_id = db_id()
     AND sd2.Create_Date        = sd1.Create_Date

  /**************************************
  * Throw Error if neither Backup or Snapshot is available
  **************************************/
  IF @SnapShotName IS NULL THROW 50001, 'No Snapshots available', 1;

  /**************************************
  * Start SQL command
  **************************************/
  SET @SqlCmd = '-- Switch database to Single User to drop all other users.'  + @nl
              + 'ALTER DATABASE [' + @DatabaseName + ']'                      + @nl
              + 'SET SINGLE_USER WITH ROLLBACK IMMEDIATE;'                    + @nl

  -- Build Command to load using Snapshot. If it fails drop the Snapshot and do database Restore
  SET @SqlCmd += 'BEGIN TRY'                                                         + @nl
               + '  -- Do Restore'                                                   + @nl
               + '  RESTORE DATABASE ' + quotename( @DatabaseName )                  + @nl
               + '  FROM DATABASE_SNAPSHOT = ''' + @SnapShotName + ''';'             + @nl
               + @nl
               + '  PRINT ''Database restored from Snapshot ' + @SnapshotName + '''' + @nl
               + 'END TRY'                                                           + @nl 
               + 'BEGIN CATCH'                                                       + @nl
               + '  PRINT ''Database restored from Snapshot ' + @SnapshotName + ' Failed''' + @nl
               + '  -- Drop Snapshot'                                                + @nl
               + '  DROP DATABASE ' + quotename( @SnapShotName ) + ';'               + @nl
               + 'END CATCH'                                                         + @nl

  SET @SqlCmd += '-- Switch database to Multi User to drop all other users.'  + @nl
              + 'ALTER DATABASE [' + @DatabaseName + ']'                      + @nl
              + 'SET MULTI_USER;'                                             + @nl

  /**************************************
  * Return Command
  **************************************/
  SELECT @SqlCmd      AS [RestoreCommand]

/**************************************************
* Main Catch Section
**************************************************/
END TRY
BEGIN CATCH
  THROW;
END CATCH
