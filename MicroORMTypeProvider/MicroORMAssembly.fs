namespace MicroORMTypeProvider

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

type Database private () = 
    static let seqToString sep (xs : #seq<_>) = String.Join(sep, xs)

    static member OpenConnection(connectionString) =
        let conn = new SqlConnection(connectionString) 
        conn.Open()
        conn

    static member Insert(table : string, columns : string[], values : obj[], conn : SqlConnection) = 
        let valueNames = columns |> Array.map (fun c -> "@" + c)
        let columnsSql = columns |> seqToString ", "
        let valueNamesSql = valueNames |> seqToString ", "
        let cmdText = sprintf "insert into %s (%s) values (%s)" table columnsSql valueNamesSql
        //printfn "--->insert sql: %s" cmdText
        //printfn "-->insert values: %A" values
        use cmd = conn.CreateCommand()
        cmd.CommandText <- cmdText
        for n, v in Array.zip columns values do
            printfn "-->parameter: %s %A" n v
            cmd.Parameters.AddWithValue(n, v) |> ignore
        cmd.ExecuteNonQuery() 

    static member Update(table : string, keyColumns : string[], keyValues : obj[], columns : string[], values : obj[], conn : SqlConnection) = 
        let updates = columns |> Seq.map (fun n -> sprintf "%s = @%s" n n) |> seqToString ", "
        let where = keyColumns |> Seq.map (fun n -> sprintf "%s = @%s" n n) |> seqToString " and "
                        
        let valueNames = columns |> Array.map (fun c -> "@" + c)
        let cmdText = sprintf "update %s set %s where %s" table updates where
        printfn "--->insert sql: %s" cmdText
        //printfn "-->insert values: %A" values
        use cmd = conn.CreateCommand()
        cmd.CommandText <- cmdText
        let allColumns = (keyColumns, columns) ||> Array.append
        let allValues = (keyValues, values) ||> Array.append
        for n, v in Array.zip allColumns allValues do
            printfn "-->parameter: %s %A" n v
            cmd.Parameters.AddWithValue(n, v) |> ignore

        cmd.ExecuteNonQuery() 
        (*
    static member Delete(table : string, columns : string[], values : obj[], conn : SqlConnection) = 
        let valueNames = columns |> Array.map (fun c -> "@" + c)
        let cmdText = sprintf "insert into %s (%s) values (%s)" table (columns |> seqToString) (valueNames |> seqToString)
        //printfn "--->insert sql: %s" cmdText
        //printfn "-->insert values: %A" values
        use cmd = conn.CreateCommand()
        cmd.CommandText <- cmdText
        for n, v in Array.zip columns values do
            printfn "-->parameter: %s %A" n v
            cmd.Parameters.AddWithValue(n, v) |> ignore
        cmd.ExecuteNonQuery() 
        *)

module internal MicroORMAssembly = 
    let seqToString sep (xs : #seq<_>) = String.Join(sep, xs)

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

    let toPascal (name : string) = 
        let name = name.ToLower().Replace("_", " ")
        let info = CultureInfo.CurrentCulture.TextInfo
        let name = info.ToTitleCase(name).Replace(" ", String.Empty)
        name

    let toPropertyStyle propertyStyle columnName = 
        match propertyStyle with
        | PropertyStyle.Pascal -> toPascal columnName
        | _ -> columnName

    let toTableName tableName = 
        toPascal tableName

    let insertSql tableName columns =
        let paramNames = columns |> Array.map (fun c -> "@" + c) |> seqToString ", "
        let columnNames = columns |> seqToString ", "
        sprintf "insert into [%s] (%s) values (%s)" tableName columnNames paramNames

    let updateSql tableName keyColumnNames valueColumnNames = 
        let updates = valueColumnNames |> Seq.map (fun n -> sprintf "%s = @%s" n n) |> seqToString ", "
        let where = keyColumnNames |> Seq.map (fun n -> sprintf "%s = @%s" n n) |> seqToString " and "
        sprintf "update [%s] set %s where %s" tableName updates where
        
    let deleteSql tableName keyColumnNames = 
        let where = keyColumnNames |> Seq.map (fun n -> sprintf "%s = @%s" n n) |> seqToString " and "
        sprintf "delete [%s] where %s" tableName where

    let emitExecuteSql sql columnNames values = 
        il {
            // let cmd = conn.CreateCommand()
            let! result = IL.declareLocal<int>()
            let! cmd = IL.declareLocal<System.Data.Common.DbCommand>()
            do! IL.ldarg_1
            do! IL.callvirt Methods.System.Data.SqlClient.SqlConnection.``CreateCommand : unit -> System.Data.Common.DbCommand``
            do! IL.stloc cmd

            // .try {
            let! try' = IL.beginExceptionBlock
            // cmd.CommandText <- "insert ...."
            do! IL.ldloc cmd
            do! IL.ldstr sql
            do! IL.dup
            do! IL.call Methods.System.Console.``Write : string -> unit``
            do! IL.callvirt Methods.System.Data.Common.DbCommand.``set_CommandText : string -> unit``

            // cmd.Parameters
            do! IL.ldloc cmd
            do! IL.callvirt Methods.System.Data.SqlClient.SqlCommand.``get_Parameters : unit -> System.Data.Common.DbParameterCollection``
                        
            for cn, v : IKVM.Reflection.Emit.PropertyBuilder in (columnNames, values) ||> List.zip do
                do! IL.dup
                do! IL.ldstr cn
                do! IL.ldarg_0
                do! IL.callvirt (v.GetGetMethod())
                do! IL.box v.PropertyType
                //do! IL.ldstr "foo"
                do! IL.callvirt Methods.System.Data.SqlClient.SqlParameterCollection.``AddWithValue : string*obj -> System.Data.SqlClient.SqlParameter``
                do! IL.pop

            do! IL.pop

            do! IL.ldloc cmd
            do! IL.callvirt Methods.System.Data.Common.DbCommand.``ExecuteNonQuery : unit -> int``
            do! IL.stloc result
            // } finally {
            do! IL.beginFinally
            do! IL.ldloc cmd
            do! IL.callvirt Methods.System.IDisposable.``Dispose : unit -> unit``
            //do! IL.en
            do! IL.endExceptionBlock
                        
            do! IL.ldloc result
            do! IL.ret
        }

    let createAssembly(connectionString, propertyStyle) = 
        let assemblyPath = Path.ChangeExtension(Path.GetTempFileName(), ".dll")
        let db = MsSqlServer(connectionString)
        let tables = db.Tables
        //let insertMethod = typeof<Database>.GetMethod("Insert", [| typeof<string>; typeof<string[]>; typeof<obj[]>; typeof<SqlConnection> |])
        //let updateMethod = typeof<Database>.GetMethod("Update", [| typeof<string>; typeof<string[]>; typeof<obj[]>; typeof<string[]>; typeof<obj[]>; typeof<SqlConnection> |])
        //printfn "--->updateMethod: %A" updateMethod

        assembly {
            for t in tables do
                let tableName = toTableName t.TableName
                do! publicType tableName {
                    let! cons = publicDefaultEmptyConstructor
                    
                    let keyColumnNames = ref []
                    let keyValues = ref []
                    let modColumnNames = ref []
                    let modValues = ref []
                    for c in t.Columns |> List.rev do
                        let propType = fromDataType c.DataType c.IsNullable
                        let columnName = toPropertyStyle propertyStyle c.ColumnName
                        let! prop = publicAutoPropertyOfType (ClrType propType) columnName { get; set; }
                        
                        if c.IsPrimaryKeyPart 
                        then keyColumnNames := c.ColumnName :: !keyColumnNames
                             keyValues := prop :: !keyValues
                        else modColumnNames := c.ColumnName :: !modColumnNames
                             modValues := prop :: !modValues

                    yield! publicMethodOfType (ClrType typeof<int>) "Insert" [ClrType typeof<SqlConnection>] {
                        let sql = insertSql t.TableName (!modColumnNames |> Seq.toArray)
                        do! emitExecuteSql sql !modColumnNames !modValues
                    }

                    yield! publicMethodOfType (ClrType typeof<int>) "Update" [ClrType typeof<SqlConnection>] {
                        let sql = updateSql t.TableName !keyColumnNames !modColumnNames
                        let allColumnNames = (!keyColumnNames, !modColumnNames) ||> List.append
                        let allValues = (!keyValues, !modValues) ||> List.append
                        do! emitExecuteSql sql allColumnNames allValues
                    }

                    yield! publicMethod<bool> "Delete" [ClrType typeof<SqlConnection>] {
                        let sql = deleteSql t.TableName (!keyColumnNames |> Seq.toArray)
                        do! emitExecuteSql sql !keyColumnNames !keyValues
                    }
                }
        } |> saveAssembly assemblyPath
        
        assemblyPath




