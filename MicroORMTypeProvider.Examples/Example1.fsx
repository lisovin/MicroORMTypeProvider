#r @"..\MicroORMTypeProvider\bin\Debug\ReflectionTypeProvider.dll"
#r @"..\MicroORMTypeProvider\bin\Debug\MicroORMTypeProvider.dll"
#r @"..\packages\Dapper.1.13\lib\net40\Dapper.dll"
#r @"..\packages\DapperExtensions.1.4.3\lib\net40\DapperExtensions.dll"
#r "System.Data"
#r "System.Transactions"
open MicroORMTypeProvider
open Dapper

let [<Literal>] connectionString = @"Data Source=(localdb)\v11.0;Initial Catalog=wiztiles;Integrated Security=True"

type Db = MicroORM<connectionString>

typeof<Db.app>.Assembly

let conn = Db.Open()

conn.Query<Db.app>("select * from app")
