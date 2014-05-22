INSERT INTO [TestTable]
           ([Id] ,[Field1] ,[Field2] ,[Field3])
		VALUES(@Id, 'T1', GETUTCDATE(), NEWID())
