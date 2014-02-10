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

type MicroORM = class end

[<TypeProvider>]
type DelegateTypeProvider(cfg : TypeProviderConfig) =
    let mutable generatedAssembly : Assembly = null 
    
    let invalidate = Event<_,_>()  

    interface IProvidedNamespace with
        member x.GetNestedNamespaces() = [||]
        member x.GetTypes() = [| typeof<MicroORM> |] 
        member x.ResolveTypeName typeName = 
            //printfn "---->ResolveTypeName: %s %A" typeName generatedAssembly
            if typeName = "MicroORM" 
            then generatedAssembly.GetExportedTypes() |> Seq.find (fun ty -> ty.FullName = typeName)
            else null
        member x.NamespaceName = "MicroORMTypeProvider"

    interface ITypeProvider with
        member __.ApplyStaticArguments(typeWithoutArguments, typeNameWithArguments, staticArguments) =
            let connectionString : string = unbox staticArguments.[0]
            let propStyle = PropertyStyle.Pascal
            let typeName = typeNameWithArguments |> Seq.last
            let assemblyPath = MicroORMAssembly.createAssembly(typeName, connectionString, propStyle)
            generatedAssembly <- Assembly.LoadFile(assemblyPath)
            let ty = generatedAssembly.GetExportedTypes() |> Seq.find (fun t -> t.FullName = typeName)
            ty

        member __.GetNamespaces() =  [| __ |]
        member __.GetStaticParameters t = 
            let p1 = 
                { new ParameterInfo() with
                    member z.Name = "ConnectionString"
                    member z.ParameterType = typeof<string> }
            [| p1 |]
        member __.GetGeneratedAssemblyContents(assembly) = 
            let bytes = System.IO.File.ReadAllBytes assembly.ManifestModule.FullyQualifiedName
            bytes
        
        member x.GetInvokerExpression(syntheticMethodBase, parameters) = 
            match syntheticMethodBase with
            | :? ConstructorInfo as ctor ->
                Expr.NewObject(ctor, Array.toList parameters) 
            | :? MethodInfo as mi ->
                if parameters.Length = 0 then
                    Expr.Call(mi, parameters |> List.ofArray)
                else
                    Expr.Call(parameters.[0], mi, Array.toList parameters.[1..])
            | _ ->
                NotImplementedException(sprintf "Not Implemented: GetInvokerExpression(%A, %A)" syntheticMethodBase parameters) |> raise   

        [<CLIEvent>]
        member __.Invalidate = invalidate.Publish

        member x.Dispose() = ()
         
        