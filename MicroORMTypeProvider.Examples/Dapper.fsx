#r @"..\MicroORMTypeProvider\bin\Debug\ReflectionTypeProvider.dll"
#r @"..\MicroORMTypeProvider\bin\Debug\MicroORMTypeProvider.dll"
#r @"..\packages\Dapper.1.13\lib\net40\Dapper.dll"
#r @"..\packages\DapperExtensions.1.4.3\lib\net40\DapperExtensions.dll"
#r "System.Data"
#r "System.Transactions"
open System
open System.Globalization
open System.Text.RegularExpressions

open MicroORMTypeProvider
open Dapper
open DapperExtensions
open DapperExtensions.Mapper
open Microsoft.FSharp.Linq.RuntimeHelpers.LeafExpressionConverter

let [<Literal>] connectionString = @"Data Source=(localdb)\v11.0;Initial Catalog=wiztiles;Integrated Security=True"

type Db = MicroORM<connectionString>

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
        fun ty columnName -> ty.GetProperty(toPascal columnName)))

let conn = Db.Open()
conn.GetList<Db.App>()
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

