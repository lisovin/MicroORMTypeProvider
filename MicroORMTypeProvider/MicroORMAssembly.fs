namespace MicroORMTypeProvider

open System
open System.Data
open System.Data.SqlClient
open System.Globalization
open System.IO

open ILBuilder
open ReflectionProvider

open MicroORMTypeProvider.Databases

open IKVM.Reflection.Emit

type FooBar() = 
    member val Test = "" with get, set

    member x.Insert() = 
        let v = if x.Test = null
                then DBNull.Value :> obj
                else x.Test :> obj
        ()

type PropertyStyle = 
| AsIs = 0
| Pascal = 1

type internal Methods = Reflected<"System.Data">

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
        sprintf "insert into [%s] (%s) values (%s); select cast (scope_identity() as int)" tableName columnNames paramNames

    let updateSql tableName keyColumnNames valueColumnNames = 
        let updates = valueColumnNames |> Seq.map (fun n -> sprintf "%s = @%s" n n) |> seqToString ", "
        let where = keyColumnNames |> Seq.map (fun n -> sprintf "%s = @%s" n n) |> seqToString " and "
        sprintf "update [%s] set %s where %s; select @@rowcount" tableName updates where
        
    let deleteSql tableName keyColumnNames = 
        let where = keyColumnNames |> Seq.map (fun n -> sprintf "%s = @%s" n n) |> seqToString " and "
        sprintf "delete [%s] where %s; select @@rowcount" tableName where

    let emitExecuteSql sql columnNames values (connection : FieldBuilder) = 
        il {
            // let cmd = conn.CreateCommand()
            let! result = IL.declareLocal<int>()
            let! cmd = IL.declareLocal<System.Data.SqlClient.SqlCommand>()
            
            do! IL.ldarg_0
            do! IL.ldfld connection

            do! IL.callvirt Methods.System.Data.SqlClient.SqlConnection.``CreateCommand : unit -> System.Data.SqlClient.SqlCommand``
            do! IL.stloc cmd

            // .try {
            let! try' = IL.beginExceptionBlock
            // cmd.CommandText <- "insert ...."
            do! IL.ldloc cmd
            do! IL.ldstr sql
            do! IL.callvirt Methods.System.Data.SqlClient.SqlCommand.``set_CommandText : string -> unit``

            // cmd.Parameters
            do! IL.ldloc cmd
            do! IL.callvirt Methods.System.Data.SqlClient.SqlCommand.``get_Parameters : unit -> System.Data.SqlClient.SqlParameterCollection``
            for cn, v : IKVM.Reflection.Emit.PropertyBuilder in (columnNames, values) ||> List.zip do
                do! IL.dup
                do! IL.ldstr cn
                do! IL.ldarg_1
                do! IL.callvirt (v.GetGetMethod())
                do! IL.box v.PropertyType
                //do! IL.castclass typeof<obj>
                // compare with null
                do! IL.dup
                let! notNull = IL.defineLabel
                do! IL.ldnull
                do! IL.call Methods.System.Object.``ReferenceEquals : obj*obj -> bool``
                do! IL.brfalse_s notNull
                do! IL.pop
                do! IL.ldnull
                do! IL.ldfld (typeof<DBNull>.GetField("Value"))
                //do! IL.box typeof<obj>
                do! IL.markLabel notNull
                do! IL.callvirt Methods.System.Data.SqlClient.SqlParameterCollection.``AddWithValue : string*obj -> System.Data.SqlClient.SqlParameter``
                
                do! IL.pop

            do! IL.pop

            do! IL.ldloc cmd
            do! IL.callvirt Methods.System.Data.SqlClient.SqlCommand.``ExecuteScalar : unit -> obj``
            do! IL.unbox_any typeof<int>
            do! IL.stloc result
            // } finally {
            do! IL.beginFinally
            do! IL.ldloc cmd
            do! IL.callvirt Methods.System.IDisposable.``Dispose : unit -> unit``
            //do! IL.en
            do! IL.endExceptionBlock
                        
            //do! IL.ldloc result
        }

    let emityEntityMethodCall entityTypes f = 
        il {
            do! IL.ldarg_1
            do! IL.callvirt Methods.System.Object.``GetType : unit -> System.Type``

            for (ty : IKVM.Reflection.Type, i, u, d) in entityTypes do
                let! typesNotEqual = IL.defineLabel
                do! IL.dup
                do! IL.ldtoken ty
                do! IL.call Methods.System.Type.``GetTypeFromHandle : System.RuntimeTypeHandle -> System.Type``
                do! IL.call Methods.System.Object.``ReferenceEquals : obj*obj -> bool``
                        
                do! IL.brfalse_s typesNotEqual
    
                do! IL.pop // pop dup-d type - the result of GetType()
                do! IL.ldarg_0
                do! IL.ldarg_1
                do! IL.unbox_any ty
                let entityMethod : IKVM.Reflection.Emit.MethodBuilder = f (i, u, d)
                do! IL.callvirt entityMethod
                //do! IL.box typeof<obj>
                do! IL.ret

                do! IL.markLabel typesNotEqual
            do! IL.pop // pop dup-d type - the result of GetType()
        }

    let createAssembly(typeName, connectionString, propertyStyle, assemblyPath) = 
        let db = MsSqlServer(connectionString)
        let tables = db.Tables

        assembly {
            yield! IL.publicType (typeName, typeof<IDisposable>) {
                let! connection = IL.privateField<SqlConnection>("connection")

                //let! dbDefaultCons = IL.privateDefaultConstructor 

                let! dbCons = IL.privateConstructor {
                    do! IL.ldarg_0
                    do! IL.callvirt Methods.System.Object.``new : unit -> obj``
                    
                    do! IL.ldarg_0
                    do! IL.ldstr connectionString
                    do! IL.newobj Methods.System.Data.SqlClient.SqlConnection.``new : string -> System.Data.SqlClient.SqlConnection``
                    do! IL.stfld connection

                    do! IL.ldarg_0
                    do! IL.ldfld connection
                    do! IL.callvirt Methods.System.Data.SqlClient.SqlConnection.``Open : unit -> unit``

                    do! IL.ret
                }

                yield! IL.publicProperty<SqlConnection> "Connection" {
                    yield! get {
                        do! IL.ldarg_0
                        do! IL.ldfld connection
                        do! IL.ret
                    }
                }

                yield! IL.overrideMethod Methods.System.IDisposable.``Dispose : unit -> unit`` {
                    do! IL.ldarg_0
                    do! IL.ldfld connection
                    do! IL.callvirt Methods.System.IDisposable.``Dispose : unit -> unit``
                    do! IL.ret
                }

                let! thisType = IL.thisType
                yield! IL.publicStaticMethod(thisType, "Connect") {
                    do! IL.newobj dbCons
                    do! IL.ret
                }

                let entityTypes = ref []
                for t in tables do
                    let tableName = toTableName t.TableName
                    let keyColumnNames = ref []
                    let keyValues = ref []
                    let modColumnNames = ref []
                    let modValues = ref []
                    let crud = ref []

                    let! entityType = IL.nestedPublicType tableName {
                        let! cons = IL.publicDefaultConstructor
                    
                        for c in t.Columns |> List.rev do
                            let propType = fromDataType c.DataType c.IsNullable
                            let columnName = toPropertyStyle propertyStyle c.ColumnName
                            let! prop = IL.publicAutoProperty(propType, columnName) { get; set; }
                        
                            if c.IsPrimaryKeyPart 
                            then keyColumnNames := c.ColumnName :: !keyColumnNames
                                 keyValues := prop :: !keyValues
                            else modColumnNames := c.ColumnName :: !modColumnNames
                                 modValues := prop :: !modValues
                        (*
                        yield! IL.publicMethod<int> "GetId" [] {
                            let idProp = !keyValues |> Seq.head
                            do! IL.ldarg_0
                            do! IL.callvirt (idProp.GetGetMethod())
                            do! IL.ret
                        }
                        *)
                    }

                    let! entityInsert = IL.publicMethod(entityType, "Insert", entityType) {
                        //let! scopeIdentity = IL.declareLocal<int>()

                        let sql = insertSql t.TableName (!modColumnNames |> Seq.toArray)
                        do! emitExecuteSql sql !modColumnNames !modValues connection
                        //do! IL.stloc scopeIdentity
                        
                        do! IL.newobj entityType
                        // set values
                        for (n,v) in (!modColumnNames, !modValues) ||> List.zip do
                            do! IL.dup
                            do! IL.ldarg_1
                            do! IL.callvirt (v.GetGetMethod())   
                            do! IL.callvirt (v.GetSetMethod())

                        // set identity (assume first in the key)
                        do! IL.dup
                        
                        do! IL.ldloc_0 // loc.0 contains the result of sql exec (from emitExecuteSql)
                        let idProp = !keyValues |> List.head
                        do! IL.callvirt (idProp.GetSetMethod())

                        do! IL.ret
                    }

                    let! entityUpdate = IL.publicMethod<bool>("Update", entityType) {
                        let sql = updateSql t.TableName !keyColumnNames !modColumnNames
                        let allColumnNames = (!keyColumnNames, !modColumnNames) ||> List.append
                        let allValues = (!keyValues, !modValues) ||> List.append
                        do! emitExecuteSql sql allColumnNames allValues connection

                        do! IL.ldloc_0 // loc.0 contains the result of sql exec (from emitExecuteSql)
                        do! IL.ldc_i4_0
                        
                        do! (IL.ifThenElse IL.bne_un_s <| il {
                            do! IL.ldc_bool false
                        } <| il {
                            do! IL.ldc_bool true
                        })
                        do! IL.ret
                    }

                    let! entityDelete = IL.publicMethod<bool>("Delete", entityType) {
                        let sql = deleteSql t.TableName (!keyColumnNames |> Seq.toArray)
                        do! emitExecuteSql sql !keyColumnNames !keyValues connection

                        do! IL.ldloc_0 // loc.0 contains the result of sql exec (from emitExecuteSql)
                        do! IL.ldc_i4_0
                        
                        do! (IL.ifThenElse IL.bne_un_s <| il {
                            do! IL.ldc_bool false
                        } <| il {
                            do! IL.ldc_bool true
                        })
                        do! IL.ret
                    }

                    entityTypes := (entityType, entityInsert, entityUpdate, entityDelete) :: !entityTypes

                yield! IL.publicMethod<obj>("Insert", typeof<obj>) {
                    do! emityEntityMethodCall !entityTypes (fun (i, u, d) -> i)

                    do! IL.ldnull
                    do! IL.ret
                }

                yield! IL.publicMethod<bool>("Update", typeof<obj>) {
                    do! emityEntityMethodCall !entityTypes (fun (i, u, d) -> u)
                    
                    do! IL.ldc_bool false
                    do! IL.ret
                }

                yield! IL.publicMethod<bool>("Delete", typeof<obj>) {
                    do! emityEntityMethodCall !entityTypes (fun (i, u, d) -> d)

                    do! IL.ldc_bool false
                    do! IL.ret
                }
            }
        } |> saveAssembly assemblyPath




