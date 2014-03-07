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
let test () = 
    use dd = db
    let app = Db.App(Icon = "/icon.png", Name = sprintf "MyApp %d" (Random().Next(1000000)), Uri = "/my/app")
    db.Insert app
   

test()

db.Connection

let o = app :> obj
db.Insert(app)
db.Insert(o)

app.Icon <- "/icon56.png"
app.AppId <- 56

db.Update(app)
db.Delete(app)

let user = Db.User() :> obj
db.Insert(user)

let til = Db.Tile() :> obj
db.Insert(til)

let screen = Db.Screen() :> obj
db.Insert(screen)

db.Insert(app :?> Db.App)

typeof<Db>.FullName

typeof<Db>.GetMethods() |> Array.map (fun mi -> mi.Name)
typeof<Db>.GetNestedTypes() |> Array.map (fun ty -> ty.FullName)

typeof<Db>.GetMethod("Insert").GetParameters()
let db = Db()

db.GetType().UnderlyingSystemType.FullName

type App = Db.App
let app = Db.App()
app.GetType().UnderlyingSystemType.FullName
app.GetType().Assembly.Location

//#r @"C:\Users\Peter\AppData\Local\Temp\tmp873F.dll"

typeof<Db.Screen>
typeof<Db>

typeof<Db.Screen>
typeof<Db>
Db.GetMembers()
typeof<string>.Get


let app = Db.App(Icon = "/icon.png", Name = "MyApp foo", Uri = "/my/app")
app.Insert
db.Insert(app)
app.GetType().Assembly
let ty = app.GetType().IsNested

let db = Db()
db.GetType().

app.GetType().
Db.Insert(app)
//type Database = Db.Database

typeof<Db.App>
typeof<Db>

let db = Db.Open()
db.Insert()


let conn = SqlConnection()
conn.Ins
//Database.Insert(conn)
Db.Insert(conn)

typeof<Db>

let conn = Db.Open()
typeof<Db.Database>.GetCustomAttributes(false)
typeof<Db.Database>.GetMethod("Insert", [| typeof<SqlConnection> |]).GetCustomAttributes(false)

Db.Database.Insert(conn)
conn
let app = Db.App(Icon = "/icon.png", Name = "MyApp", Uri = "/my/app")
app.Insert(conn)

app.AppId <- 48
app.Icon <- "/icon.png/bar"
app.Name <- sprintf "Name %d" (Random().Next(1000000))
app.Uri<- "/my/app/1"


#r @"..\packages\Dapper.1.13\lib\net40\Dapper.dll"
open System.Globalization
open System.Text.RegularExpressions

open Dapper
open Microsoft.FSharp.Linq.RuntimeHelpers.LeafExpressionConverter



let app = Db.App()
app.AppId <- 48
app.Icon <- "/icon.png/bar"
app.Name <- sprintf "Name %d" (Random().Next(1000000))
app.Uri<- "/my/app/1"



app.Name <- sprintf "Name %d" (Random().Next(1000000))
app
app.Update(conn)
app.AppId <- 100



app.Delete(conn)

app.GetType().Assembly.Location

app.AppId <- 7
app.Icon <- "/icon.png 2" 
app.Name <- "MyApp 3 kjh"
app.Uri<- "/my/app 2"

app.Update(conn)

app.Delete(conn)



typeof<Db.App>.AssemblyQualifiedName
typeof<Db.App>.GetMethods(System.Reflection.BindingFlags.Static) |> Array.filter (fun mi -> mi.Name = "Insert")
let app = (Db.App())
Db.App.App.Insert(Db.App.App())

Db.App.Insert(Db.App())

let toPascal (name : string) = 
    let name = name.ToLower().Replace("_", " ")
    let info = CultureInfo.CurrentCulture.TextInfo
    let name = info.ToTitleCase(name).Replace(" ", String.Empty)
    name

let fromPascal (name : string) = 
    Regex.Replace(name, "(?<=.)([A-Z])", "_$0", RegexOptions.Compiled).ToLower()

type OrmMapper<'T when 'T : not struct>() =
    inherit ClassMapper<'T>()

    do let entityName = typeof<'T>.Name
       let keyProp = typeof<'T>.GetProperty(entityName + "Id")
       //base.Map(keyProp).Key(KeyType.Identity)//.Column(entityName.ToLower() + "_id") |> ignore
       for p in typeof<'T>.GetProperties() do
            let m = base.Map(p).Column(fromPascal p.Name)
            if p = keyProp 
            then m.Key(KeyType.Identity) |> ignore
        

DapperExtensions.DapperExtensions.DefaultMapper <- typedefof<OrmMapper<_>>

SqlMapper.SetTypeMap(
    typeof<Db.App>,
    CustomPropertyTypeMap(
        typeof<Db.App>, 
        fun ty columnName -> 
            printfn "--->sqlmapper: %s %s %A"  columnName (toPascal columnName) (ty.GetProperty(toPascal columnName))
            ty.GetProperty(toPascal columnName)))

let conn = Db.Open()
conn.GetList<Db.App>()
conn.Query<Db.App>("select * from app")
conn.GetList<Db.Tile>() |> Seq.toArray
let app = conn.Get<Db.App>(1)
app.Icon <- app.Icon + "_modified"
conn.Update(app)
conn.Get<Db.App>(1)

conn.Query("""
    select * 
    from app a
    inner join [user] u on 1 = 1
""", (fun (app : Db.App) (user : Db.User) -> app, user), splitOn = "user_id")

