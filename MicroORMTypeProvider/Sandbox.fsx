#r @"bin\Debug\IKVM.Reflection.dll"
#r @"bin\Debug\ILBuilder.dll"
#r @"bin\Debug\ReflectionTypeProvider.dll"
#load "MsSqlServer.fs"
#load "MicroORMAssembly.fs"

open System
open System.IO
open MicroORMTypeProvider

let assemblyFileName = Path.ChangeExtension(Path.GetTempFileName(), ".dll")

let [<Literal>] connectionString = @"Data Source=(localdb)\v11.0;Initial Catalog=wiztiles;Integrated Security=True"
MicroORMAssembly.createAssembly("Db", connectionString, PropertyStyle.Pascal, assemblyFileName)

printfn "--->assembly: %s" assemblyFileName

let v = typeof<DBNull>.GetField("Value")
let r = v.GetValue(null) :?> DBNull
r = DBNull.Value