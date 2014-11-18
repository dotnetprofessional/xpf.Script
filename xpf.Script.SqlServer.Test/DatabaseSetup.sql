/*
	You must run this script to create the database used for running 
	the unit tests. This database is not required for general use of the
	libary.
*/

CREATE DATABASE xpfScript
Go
USE xpfIOScript
GO
/****** Object:  Table [dbo].[TestTable]    Script Date: 06/18/2009 14:18:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[TestTable](
	[Id] [int] NOT NULL,
	[Field1] [varchar](50) NOT NULL,
	[Field2] [datetime] NOT NULL,
	[Field3] [uniqueidentifier] NOT NULL,
 CONSTRAINT [PK_TestTable] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING OFF
GO
/****** Object:  Default [DF_TestTable_Field2]    Script Date: 06/18/2009 14:18:42 ******/
ALTER TABLE [dbo].[TestTable] ADD  CONSTRAINT [DF_TestTable_Field2]  DEFAULT (getdate()) FOR [Field2]
GO
/****** Object:  Default [DF_TestTable_Field3]    Script Date: 06/18/2009 14:18:42 ******/
ALTER TABLE [dbo].[TestTable] ADD  CONSTRAINT [DF_TestTable_Field3]  DEFAULT (newid()) FOR [Field3]
GO

/*
	Sample data used in tests. Should the data become corrupt while testing simply drop the database and rerun this complete script
*/
INSERT INTO TestTable
		([Id], Field1 ,[Field2] ,[Field3])
     VALUES
		(1, 'Record 1', '2009-06-17 14:51:05.843', CAST('7DA76C33-F239-4B08-B7EB-D363D3892AF1' AS UNIQUEIDENTIFIER))

INSERT INTO TestTable
		([Id], Field1 ,[Field2] ,[Field3])
     VALUES
		(2, 'Record 2', '2009-06-17 14:51:11.497', CAST('D3AC8300-9DB3-449B-8938-09CA4663379F' AS UNIQUEIDENTIFIER))

INSERT INTO TestTable
		([Id], Field1 ,[Field2] ,[Field3])
     VALUES
		(3, 'Record 3', '2009-06-17 14:51:16.423', CAST('C9E2593E-E131-4E7F-91C1-9A6A85CC5333' AS UNIQUEIDENTIFIER))				

-- Table used to test transforming data to class instances
CREATE TABLE [dbo].[DataTypeCheckTable](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[NonNullString] [varchar](50) NOT NULL,
	[NullableString] [varbinary](50) NULL,
	[NonNullNumeric] [int] NOT NULL,
	[NullableNumeric] [int] NULL,
	[NonNullDateTime] [datetime] NOT NULL,
	[NullableDateTime] [datetime] NULL,
 CONSTRAINT [PK_DataTypeCheckTable] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

INSERT INTO DataTypeCheckTable
		(NonNullString, NullableString, NonNullNumeric, NullableNumeric, NonNullDateTime, NullableDateTime)
	VALUES
		('Some Text', null, 100, null, '2011-01-01', null)

INSERT INTO DataTypeCheckTable
		(NonNullString, NullableString, NonNullNumeric, NullableNumeric, NonNullDateTime, NullableDateTime)
	VALUES
		('Some Text 2', null, 200, null, '2011-01-02', null)
