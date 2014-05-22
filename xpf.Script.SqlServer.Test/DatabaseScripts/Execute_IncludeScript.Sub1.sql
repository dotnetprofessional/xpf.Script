INSERT INTO [TestTable]
           ([Id] ,[Field1] ,[Field2] ,[Field3])
		VALUES(11, 'T1', GETUTCDATE(), NEWID())


include Execute_IncludeScript.Sub2.sql
