#r @"..\MicroORMTypeProvider\bin\Debug\ReflectionTypeProvider.dll"
#r @"..\MicroORMTypeProvider\bin\Debug\MicroORMTypeProvider.dll"
#r "System.Data"
#r "System.Transactions"
open MicroORMTypeProvider
let [<Literal>] connectionString = @"Data Source=(localdb)\v11.0;Initial Catalog=wiztiles;Integrated Security=True"
type Db = MicroORM<connectionString>

Db.Open()
Db.app()
Db.
let con = Db.Open()
con.BeginTransaction()
con.Open()
con.Dispose()
con.StatisticsEnabled
con.GetHashCode()