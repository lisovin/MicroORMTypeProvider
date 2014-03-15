#r @"..\MicroORMTypeProvider\bin\Debug\ReflectionTypeProvider.dll"
#r @"..\MicroORMTypeProvider\bin\Debug\MicroORMTypeProvider.dll"
#r "System.Data"
#r "System.Transactions"

open System
open System.Data.SqlClient
open MicroORMTypeProvider

let [<Literal>] connectionString = @"Data Source=(localdb)\v11.0;Initial Catalog=wiztiles;Integrated Security=True"

type Db = MicroORM<connectionString>

let db = Db.Connect()

let tile = Db.Tile()
tile.AppId <- 1
tile.ScreenId <- 1
db.Insert(tile)

(db :> IDisposable).Dispose()

db.Connection.Query("""
    select * 
    from app a
    inner join [user] u on 1 = 1
""", (fun (app : Db.App) (user : Db.User) -> app, user), splitOn = "user_id")

