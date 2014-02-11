namespace MicroORMTypeProvider

open System
open System.Collections.Generic
open System.Reflection
open System.IO

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
        printfn "--->generate assembly"
        let connectionString : string = unbox args.[0]
        let propStyle = PropertyStyle.Pascal
        let assemblyPath = MicroORMAssembly.createAssembly("Main", connectionString, propStyle)
        let generatedAssembly = ProvidedAssembly.RegisterGenerated(assemblyPath)
        //let ty = generatedAssembly.GetTypes() |> Seq.find (fun t -> t.FullName = typeName)
        let ty = generatedAssembly.GetType("Main")
        let mi = ty.GetMethod("Open")
        printfn "--->returning type: %A %A" ty mi
        let ty = ProvidedTypeDefinition(assembly, ns, typeName, Some typeof<obj>, IsErased = false, HideObjectMethods = true)
        ty.AddMembers(generatedAssembly.GetExportedTypes() |> Seq.filter (fun t -> t.Name <> "Main") |> Seq.toList)
        let openMethod = ProvidedMethod("Open", [], typeof<System.Data.SqlClient.SqlConnection>)
        openMethod.AddMethodAttrs(MethodAttributes.Static ||| MethodAttributes.Public)
        openMethod.InvokeCode <- fun args -> <@@ new System.Data.SqlClient.SqlConnection(connectionString) @@>
        ty.AddMember(openMethod)
        let ass = ProvidedAssembly(Path.ChangeExtension(Path.GetTempFileName(), ".dll"))
        ass.AddTypes([ty])
        //ass.AddTypes(assembly.GetExportedTypes() |> Seq.toList)
        ty


    do containerType.DefineStaticParameters(parameters, buildAssembly)
       this.AddNamespace(ns, [containerType])
(*
type internal MicroORM() = inherit obj()

[<TypeProvider>]
type DelegateTypeProvider(cfg : TypeProviderConfig) =
    let mutable generatedAssemblyBytes = null
    let mutable generatedAssembly = null 
    
    let invalidate = Event<_,_>()  

    interface IProvidedNamespace with
        member x.GetNestedNamespaces() = [||]
        member x.GetTypes() = 
            printfn "---->GetTypes(): %A" typeof<MicroORM>
            [| typeof<MicroORM> |] 
        member x.ResolveTypeName typeName = 
            printfn "---->ResolveTypeName: %s %A" typeName generatedAssembly
            if typeName = "MicroORM" 
            then typeof<MicroORM>
            //generatedAssembly.GetExportedTypes() |> Seq.find (fun ty -> ty.FullName = typeName)
            else null
        member x.NamespaceName = "MicroORMTypeProvider"

    interface ITypeProvider with
        member __.ApplyStaticArguments(typeWithoutArguments, typeNameWithArguments, staticArguments) =
            printfn "---->ApplyStaticArguments(%A, %A, %A)" typeWithoutArguments typeNameWithArguments staticArguments
            if typeWithoutArguments = typeof<MicroORM> then
                printfn "--->generate assembly"
                let connectionString : string = unbox staticArguments.[0]
                let propStyle = PropertyStyle.Pascal
                let typeName = typeNameWithArguments |> Seq.last
                let assemblyPath = MicroORMAssembly.createAssembly(typeName, connectionString, propStyle)
                generatedAssemblyBytes <- File.ReadAllBytes(assemblyPath)
                generatedAssembly <- Assembly.Load(generatedAssemblyBytes)
                //let ty = generatedAssembly.GetTypes() |> Seq.find (fun t -> t.FullName = typeName)
                let ty = generatedAssembly.GetType(typeName)
                printfn "--->returning type: %A" ty
                ty
                //let pt = ProvidedTypeDefinition(__.GetType().Assembly, "MicroORMTypeProvider", typeName, None, IsErased = false) 
                //pt.AddMember(ty)
                //pt :> Type
            else null

        member __.GetNamespaces() =  [| __ |]
        member __.GetStaticParameters t = 
            let p1 = 
                { new ParameterInfo() with
                    member z.Name = "ConnectionString"
                    member z.ParameterType = typeof<string>
                    member z.Attributes with get() = ParameterAttributes.Optional }
            [| p1 |]
        member __.GetGeneratedAssemblyContents(assembly) = 
            printfn "--->GetGeneratedAssembly: %A" assembly
            generatedAssemblyBytes
        
        member x.GetInvokerExpression(syntheticMethodBase, parameters) = 
            printfn "---->GetInvokerExpression(%A, %A)" syntheticMethodBase parameters
            match syntheticMethodBase with
            | :? ConstructorInfo as ctor ->
                Expr.NewObject(ctor, Array.toList parameters) 
            | :? MethodInfo as mi ->
                if parameters.Length = 0 then
                    Expr.Call(mi, parameters |> List.ofArray)
                else
                    Expr.Call(parameters.[0], mi, Array.toList parameters.[1..])
            | _ ->
                printfn "---->not implemented GetInvokerExpression(%A, %A)" syntheticMethodBase parameters
                NotImplementedException(sprintf "Not Implemented: GetInvokerExpression(%A, %A)" syntheticMethodBase parameters) |> raise   

        [<CLIEvent>]
        member __.Invalidate = invalidate.Publish

        member x.Dispose() = ()
         
*)        