xpf.Script
==========

Is a cross platform scripting abstraction for scripting engines such as SQL Server, PowerShell, Razor or Http. With SQL Server being the only currently supported scripting engine. Demand for additional engines can be added with community support.

The following code demonstrates how you can execute a simple SQL Script using the library:

``` CSharp
 var result = new Script()
     .Database() // Specifies to use the SQL Server Scripting engine
     .UsingCommand("SELECT @RowCount = COUNT(*) FROM Customer WHERE Name = @Name")
     .WithIn(new {Name = "John"})
     .WithOut(new { RowCount = DbType.Int32})
     .Execute();
``` 

The library takes care of defining input and output parameters and provides a more natural way to define them. The library supports the ability to use embedded resource files as script input and have them reference other resource files allowing for easy reuse of scripts.

__Scripts.VerifyCustomerRecord.sql__
  ``` SQL

  IF(EXISTS(SELECT 1 FROM Customer WHERE CustomerId = @CustomerId))
    RAISE ERROR (1,23,'Customer Already exists')
```
__Scripts.AddCustomerRecord.sql__
``` SQL
  :r Scripts.VerifyCustomerRecord.sql
  
  INSERT INTO Customer(@CustomerId, CustomerName, Address)
        VALUES(@CustomerId, @CustomerName, @Address)
```

__Execute scripts__
``` CSharp
 var result = new Script()
     .Database() // Specifies to use the SQL Server Scripting engine
     .UsingScript("Scripts.AddCustomerRecord.sql")
     .WithIn(new {CustomerId = 2, CustomerName = "John", Address = "124 Street"})
     .Execute();
``` 

See the [WIKI](https://github.com/dotnetprofessional/xpf.Script/wiki) pages for more documentation on how to use the library.
