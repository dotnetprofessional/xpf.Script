-- This query is designed to trigger more than one row returned in a data reader
select * from (
SELECT        TestTable.Id, TestTable.Field1, TestTable.Field2, TestTable.Field3
FROM            TestTable CROSS JOIN
                         TestTable AS TestTable_2 CROSS JOIN
                         TestTable AS TestTable_1
						 )
T FOR XML PATH('TestTable'), ROOT('ArrayOfTestTable')

