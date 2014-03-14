namespace MicroORMTypeProvider.Databases

open System
open System.Data.SqlClient
open Microsoft.FSharp.Linq.NullableOperators

type internal MsSqlServerTableColumn() = 
    member val table_name = "" with get, set
    member val column_name = "" with get, set
    member val data_type = "" with get, set
    member val is_nullable = "" with get, set
    member val is_primary_key_part = Nullable(0) with get, set

type internal Column = {
    ColumnName : string
    DataType : string
    IsNullable : bool
    IsPrimaryKeyPart : bool
}

type internal Table = {
    TableName : string
    Columns : Column list
}

type internal MsSqlServer(connectionString) =
    let tablesSql = """
select 
    t.table_name, 
    c.column_name, 
    c.is_nullable, 
    c.data_type, 
    isnull(objectproperty(object_id(k1.constraint_name), 'IsPrimaryKey'), 0) is_primary_key_part,
	k2.table_name as referenced_table_name
from information_schema.tables t
inner join information_schema.columns c on t.table_name = c.table_name
left join information_schema.key_column_usage k1 on k1.table_name = t.table_name and k1.column_name = c.column_name
left join information_schema.referential_constraints rc1 on rc1.constraint_name = k1.constraint_name
left join information_schema.key_column_usage k2 on k2.constraint_name = rc1.unique_constraint_name
order by t.table_name, c.column_name
    """

    member __.Tables
        with get() = 
            use conn = new SqlConnection(connectionString)
            conn.Open()
            use cmd = conn.CreateCommand()
            cmd.CommandText <- tablesSql
            use reader = cmd.ExecuteReader()
            seq { while reader.Read() do
                    yield MsSqlServerTableColumn(
                        table_name = unbox(reader.["table_name"]),
                        column_name = unbox(reader.["column_name"]),
                        data_type = unbox(reader.["data_type"]),
                        is_nullable = unbox(reader.["is_nullable"]),
                        is_primary_key_part = unbox(reader.["is_primary_key_part"])
                    )
            } 
            |> Seq.groupBy (fun t -> t.table_name)
            |> Seq.map (fun (tableName, tableColumns) ->
                let columns  = 
                    tableColumns 
                    |> Seq.map (fun tc -> {
                                            ColumnName = tc.column_name
                                            DataType = tc.data_type
                                            IsNullable = "YES".Equals(tc.is_nullable, StringComparison.InvariantCultureIgnoreCase)
                                            IsPrimaryKeyPart = (tc.is_primary_key_part ?= 1)
                                            })
                    |> Seq.toList
                {TableName = tableName; Columns = columns})
            |> Seq.toList 
