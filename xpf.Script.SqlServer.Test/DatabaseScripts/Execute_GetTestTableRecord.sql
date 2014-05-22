-- Return a record from the TestTable for verifying tests
SELECT @outParam1 = Id, @outParam2 = Field1, @outParam3 = Field2 FROM TestTable where Id = @param1
