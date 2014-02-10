#r "System.Transactions"
#r @"..\packages\Dapper.1.12.1\lib\net40\Dapper.dll"
#r @"..\packages\DapperExtensions.1.4.3\lib\net40\DapperExtensions.dll"

open System
open System.Collections.Generic
open System.Data.SqlClient

open Dapper
open DapperExtensions

type App() = 
    member val AppId = 0 with get, set
    member val Name = "" with get, set
    member val Icon = "" with get, set

type Table() = 
    member val table_catalog = "" with get, set
    member val table_schema = "" with get, set
    member val table_name = "" with get, set

let test() = 
    use conn = new SqlConnection(@"Data Source=(localdb)\v11.0;Initial Catalog=wiztiles;Integrated Security=True")
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "select * from app"
    use reader = cmd.ExecuteReader()
    seq {
        while (reader.Read()) do
            yield App(AppId = (reader.["app_id"] :?> int), 
                      Name = (reader.["name"] :?> string))
    } |> Seq.toArray

test()
    //conn.Get<App>(1)


let conn = new SqlConnection(@"Data Source=(localdb)\v11.0;Initial Catalog=wiztiles;Integrated Security=True")
conn.Open()
let app = conn.Query<App>("select * from app") |> Seq.head
app.App_Id
let rs = conn.Query<Table>("SELECT * FROM information_schema.tables") |> Seq.toArray
rs.[0].table_name


apps
----
