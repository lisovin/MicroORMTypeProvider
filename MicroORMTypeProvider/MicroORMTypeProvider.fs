namespace MicroORMTypeProvider

open System
open System.Collections.Generic
open System.Reflection
open System.IO
open System.Data.SqlClient

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations

open MicroORMAssembly

[<TypeProviderAssembly>] 
do()

[<TypeProvider>]
type MicroORMTypeProvider(cfg : TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    let ns = "MicroORMTypeProvider"
    let assembly = Assembly.GetExecutingAssembly()
    let containerType = ProvidedTypeDefinition(assembly, ns, "MicroORM", None, HideObjectMethods = true, IsErased = false)

    let parameters = [
        ProvidedStaticParameter("ConnectionString", typeof<string>)
    ]

    let buildAssembly typeName (args : obj[]) = 
        let ty = ProvidedTypeDefinition(assembly, ns, typeName, None, IsErased = false, HideObjectMethods = true)
        let connectionString : string = unbox args.[0]
        let propStyle = PropertyStyle.Pascal
        let assemblyPath = MicroORMAssembly.createAssembly(connectionString, propStyle)
        let generatedAssembly = ProvidedAssembly.RegisterGenerated(assemblyPath)

        let ty = ProvidedTypeDefinition(assembly, ns, typeName, Some typeof<obj>, IsErased = false, HideObjectMethods = true)
        ty.AddMembers(generatedAssembly.GetExportedTypes() |> Seq.toList)

        let openCode args = 
            <@@ 
                let conn = new SqlConnection(connectionString) 
                conn.Open()
                conn 
            @@>
        let openMethod = ProvidedMethod("Open", [], typeof<SqlConnection>, IsStaticMethod = true, InvokeCode = openCode)

        ty.AddMembers([openMethod])

        let ass = ProvidedAssembly(Path.ChangeExtension(Path.GetTempFileName(), ".dll"))
        ass.AddTypes([ty])
        ty


    do base.RegisterRuntimeAssemblyLocationAsProbingFolder(cfg)
       containerType.DefineStaticParameters(parameters, buildAssembly)
       this.AddNamespace(ns, [containerType])