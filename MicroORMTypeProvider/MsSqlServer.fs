namespace MicroORMTypeProvider.Databases

open System
open System.Data.SqlClient

type internal MsSqlServerTableColumn() = 
    member val table_name = "" with get, set
    member val column_name = "" with get, set
    member val data_type = "" with get, set
    member val is_nullable = "" with get, set

type internal Column = {
    ColumnName : string
    DataType : string
    IsNullable : bool
}

type internal Table = {
    TableName : string
    Columns : Column list
}

type internal MsSqlServer(connectionString) =
    let tablesSql = """
select t.table_name, c.column_name, c.is_nullable, c.data_type
from information_schema.tables t
inner join information_schema.columns c on t.TABLE_NAME = c.TABLE_NAME
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
                        is_nullable = unbox(reader.["is_nullable"])
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
                                            })
                    |> Seq.toList
                {TableName = tableName; Columns = columns})
            |> Seq.toList 
