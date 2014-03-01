#r "System.Transactions"

open System
open System.Collections.Generic
open System.Data.SqlClient

let sql = "insert app (name, icon) values ('asdf22', 'asd'); select @@rowcount"
let conn = new SqlConnection(@"Data Source=(localdb)\v11.0;Initial Catalog=wiztiles;Integrated Security=True")
conn.Open()
let cmd = conn.CreateCommand()
cmd.CommandText <- sql
let r = cmd.ExecuteScalar()

r :?> int
r.GetType().FullName
let tableSql = """
select 
    t.table_name, 
    c.column_name, 
    c.is_nullable, 
    c.data_type, 
    isnull(objectproperty(object_id(k.constraint_name), 'IsPrimaryKey'), 0) is_primary_key_part
from information_schema.tables t
inner join information_schema.columns c on t.TABLE_NAME = c.TABLE_NAME
left join information_schema.key_column_usage k on k.table_name = t.table_name and k.column_name = c.column_name
    """

let test() = 
    use conn = new SqlConnection(@"Data Source=(localdb)\v11.0;Initial Catalog=wiztiles;Integrated Security=True")
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- tableSql
    use reader = cmd.ExecuteReader()
    seq {
        while (reader.Read()) do
            yield reader.["column_name"], reader.["is_primary_key_part"]
    } |> Seq.toArray

let rs = test()

let p1 = rs.[0] |> snd
let p2 = rs.[1] |> snd
p2.GetType().FullName

p1 :?> Nullable<int>
p2 :?> Nullable<int>

p1 :? int
p2 :? int

p2 ?= 1
    //conn.Get<App>(1)


