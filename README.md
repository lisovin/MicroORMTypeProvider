MicroORMTypeProvider
====================

F# generative type provider that simplifies using micro-ORMs (e.g. Dapper, etc.) by generating types mapped to tables.

```fsharp
#r @"..\MicroORMTypeProvider\bin\Debug\ReflectionTypeProvider.dll"
#r @"..\MicroORMTypeProvider\bin\Debug\MicroORMTypeProvider.dll"
#r "System.Data"
#r "System.Transactions"

open System
open MicroORMTypeProvider

let [<Literal>] connectionString = @"Data Source=(localdb)\v11.0;Initial Catalog=test;Integrated Security=True"
type Db = MicroORM<connectionString>

let conn = Db.Open()

let user = Db.User(Name = "John Doe", Age = 30)
user.Insert(conn)

user.Age <- 40
use.Update(conn)

user.Delete(conn)
