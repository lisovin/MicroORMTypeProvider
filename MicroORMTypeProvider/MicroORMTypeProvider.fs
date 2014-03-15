namespace MicroORMTypeProvider

open System
open System.Collections.Generic
open System.Reflection
open System.IO
open System.Data.SqlClient

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations

open MicroORMAssembly

type MicroORM = class end

[<TypeProviderAssembly>] 
do()

[<TypeProvider>]
type MicroORMTypeProvider(cfg : TypeProviderConfig) =
    let ns = "MicroORMTypeProvider"
    let invalidate = Event<_,_>()  
    let mutable generatedAssemblyBytes = null

    interface IProvidedNamespace with
        member x.GetNestedNamespaces() = [||]

        member x.GetTypes() = [| typeof<MicroORM> |] 
        
        member x.ResolveTypeName typeName = 
            if typeName = "MicroORM" 
            then typeof<MicroORM>
            else null
        
        member x.NamespaceName = ns

    interface ITypeProvider with
        member __.ApplyStaticArguments(typeWithoutArguments, typeNameWithArguments, staticArguments) =
            let typeName = typeNameWithArguments |> Seq.last
            
            let connectionString : string = unbox staticArguments.[0]
            let propStyle = PropertyStyle.Pascal
            let assemblyFileName = Path.ChangeExtension(Path.GetTempFileName(), ".dll")
            //printfn "--->assembly: %s" assemblyFileName
            MicroORMAssembly.createAssembly(typeName, connectionString, propStyle, assemblyFileName)
            let generatedAssembly = Assembly.LoadFrom assemblyFileName
            generatedAssemblyBytes <- File.ReadAllBytes(assemblyFileName)
            
            let ty = generatedAssembly.GetType(typeName)
            ty

        member __.GetNamespaces() =  [| __ |]

        override this.GetStaticParameters(ty) =
            if ty.Name = "MicroORM" 
            then [| { new ParameterInfo() with
                        member __.Name = "ConnectionString"
                        member __.ParameterType = typeof<string>
                        member __.Attributes with get() = ParameterAttributes.Optional } |]
            else [| |]

        member __.GetGeneratedAssemblyContents(assembly) = 
            generatedAssemblyBytes
        
        
        member x.GetInvokerExpression(syntheticMethodBase, parameters) = 
            match syntheticMethodBase with
            | :? ConstructorInfo as ctor ->
                Expr.NewObject(ctor, Array.toList parameters) 
            | :? MethodInfo as mi ->
                if mi.IsStatic then 
                    Quotations.Expr.Call(mi, Array.toList parameters) 
                else
                    Quotations.Expr.Call(parameters.[0], mi, Array.toList parameters.[1..])
            | _ ->
                NotImplementedException(sprintf "Not Implemented: GetInvokerExpression(%A, %A)" syntheticMethodBase parameters) |> raise 

        [<CLIEvent>]
        member __.Invalidate = invalidate.Publish

        member x.Dispose() = ()