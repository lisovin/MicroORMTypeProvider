module MicroORMAssembly

open System
open System.Data
open System.Data.SqlClient
open System.Globalization
open System.IO

open ILBuilder
open ReflectionProvider

open MicroORMTypeProvider.Databases

type PropertyStyle = 
| AsIs = 0
| Pascal = 1

type internal Methods = Reflected<"System.Data">

module internal MicroORMAssembly = 
    let fromDataType dataType isNullable = 
        let clrType = 
            match SqlDbType.Parse(typeof<SqlDbType>, dataType, true) :?> SqlDbType with
            | SqlDbType.BigInt -> typeof<Int64>
            | SqlDbType.VarBinary -> typeof<Byte[]>
            | SqlDbType.Bit -> typeof<Boolean>
            | SqlDbType.Char -> typeof<String>
            | SqlDbType.Date -> typeof<DateTime>
            | SqlDbType.DateTime -> typeof<DateTime>
            | SqlDbType.DateTime2 -> typeof<DateTime>
            | SqlDbType.DateTimeOffset -> typeof<DateTimeOffset>
            | SqlDbType.Decimal -> typeof<Decimal>
            | SqlDbType.Float -> typeof<Double>
            | SqlDbType.Binary -> typeof<Byte[]>
            | SqlDbType.Int -> typeof<Int32>
            | SqlDbType.Money -> typeof<Decimal>
            | SqlDbType.NChar -> typeof<String>
            | SqlDbType.NText -> typeof<String>
            | SqlDbType.NVarChar -> typeof<String>
            | SqlDbType.Real -> typeof<Single>
            | SqlDbType.Timestamp -> typeof<Byte[]>
            | SqlDbType.SmallInt -> typeof<Int16>
            | SqlDbType.SmallMoney -> typeof<Decimal>
            | SqlDbType.Variant -> typeof<obj>
            | SqlDbType.Text -> typeof<String>
            | SqlDbType.Time -> typeof<TimeSpan>
            | SqlDbType.TinyInt -> typeof<Byte>
            | SqlDbType.UniqueIdentifier -> typeof<Guid>
            | SqlDbType.VarChar -> typeof<String>
            | SqlDbType.Xml -> typeof<string>
            | SqlDbType.Image -> typeof<byte[]>
            | SqlDbType.SmallDateTime -> typeof<DateTime>
            | _ -> typeof<obj>
        if isNullable && clrType.IsValueType 
        then typeof<Nullable<_>>.GetGenericTypeDefinition().MakeGenericType([|clrType|])
        else clrType

    let toPropertyStyle propertyStyle columnName = 
        let toPascal (name : string) = 
            let name = name.ToLower().Replace("_", " ")
            let info = CultureInfo.CurrentCulture.TextInfo
            let name = info.ToTitleCase(name).Replace(" ", String.Empty)
            name

        match propertyStyle with
        | PropertyStyle.Pascal -> toPascal columnName
        | _ -> columnName

    let createAssembly(rootTypeName, connectionString, propertyStyle) = 
        // printfn "--->create assembly: %s" connectionString
        let assemblyPath = Path.ChangeExtension(Path.GetTempFileName(), ".dll")
        let db = MsSqlServer(connectionString)
        let tables = db.Tables
        assembly assemblyPath {
            do! publicType rootTypeName {
                do! publicStaticMethod<SqlConnection> "Open" [] {
                    ldstr connectionString
                    newobj (Constructor Methods.System.Data.SqlClient.SqlConnection.``new : string -> System.Data.SqlClient.SqlConnection``)
                    ret
                }
            }
            for t in tables do
                do! publicType t.TableName {
                    do! publicDefaultEmptyConstructor

                    for c in t.Columns do
                        let propType = fromDataType c.DataType c.IsNullable
                        let columnName = toPropertyStyle propertyStyle c.ColumnName
                        do! publicAutoPropertyOfType propType columnName { get; set; }
                }
        } |> saveAssembly
        
        assemblyPath




