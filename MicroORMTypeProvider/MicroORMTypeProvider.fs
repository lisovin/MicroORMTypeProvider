namespace MicroORMTypeProvider

open System
open System.Collections.Generic
open System.Reflection
open System.IO
open System.Data.SqlClient

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations

open MicroORMAssembly

type MicroORM() = inherit obj()
type Db = class end


[<TypeProviderAssembly>] 
do()

[<TypeProvider>]
type MicroORMTypeProvider(cfg : TypeProviderConfig) =
    let ns = "MicroORMTypeProvider"
    let mutable generatedAssemblyBytes = null
    let mutable generatedAssembly = null 
    
    let invalidate = Event<_,_>()  

    interface IProvidedNamespace with

        member x.GetNestedNamespaces() = [||]

        member x.GetTypes() = 
            printfn "---->GetTypes(): %A" typeof<MicroORM>
            //types |> Seq.toArray
            [| typeof<MicroORM> |] 
        
        member x.ResolveTypeName typeName = 
            printfn "---->ResolveTypeName: %s %A" typeName generatedAssembly
            if typeName = "MicroORM" 
            then typeof<MicroORM>
            //generatedAssembly.GetExportedTypes() |> Seq.find (fun ty -> ty.FullName = typeName)
            else null
        
        member x.NamespaceName = ns

    interface ITypeProvider with
        member __.ApplyStaticArguments(typeWithoutArguments, typeNameWithArguments, staticArguments) =
            let typeName = typeNameWithArguments |> Seq.last
            
            let connectionString : string = unbox staticArguments.[0]
            let propStyle = PropertyStyle.Pascal
            let assemblyFileName = Path.ChangeExtension(Path.GetTempFileName(), ".dll")
            MicroORMAssembly.createAssembly(typeName, connectionString, propStyle, assemblyFileName)
            generatedAssembly <- Assembly.LoadFrom assemblyFileName
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
            printfn "--->GetGeneratedAssembly: %A" assembly.ManifestModule.FullyQualifiedName
            generatedAssemblyBytes
        
        
        member x.GetInvokerExpression(syntheticMethodBase, parameters) = 
            printfn "---->GetInvokerExpression(%A, %A)" syntheticMethodBase parameters
            match syntheticMethodBase with
            | :? ConstructorInfo as ctor ->
                Expr.NewObject(ctor, Array.toList parameters) 
            | :? MethodInfo as mi ->
                if mi.IsStatic then 
                    Quotations.Expr.Call(mi, Array.toList parameters) 
                else
                    Quotations.Expr.Call(parameters.[0], mi, Array.toList parameters.[1..])
            | _ ->
                printfn "---->not implemented GetInvokerExpression(%A, %A)" syntheticMethodBase parameters
                //NotImplementedException(sprintf "Not Implemented: GetInvokerExpression(%A, %A)" syntheticMethodBase parameters) |> raise 
                Expr.Value(null)

        [<CLIEvent>]
        member __.Invalidate = invalidate.Publish

        member x.Dispose() = ()

(*

[<TypeProviderAssembly>] 
do()

[<TypeProvider>]
type MicroORMTypeProvider(cfg : TypeProviderConfig) as this = 
    //inherit TypeProviderForNamespaces()

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
        //let assemblyPath = MicroORMAssembly.createAssembly(typeName, connectionString, propStyle)
        //let generatedAssembly = ProvidedAssembly.RegisterGenerated(assemblyPath)

        let ty = ProvidedTypeDefinition(assembly, ns, typeName, Some typeof<obj>, IsErased = false, HideObjectMethods = true)
        //ty.AddMembers(generatedAssembly.GetExportedTypes() |> Seq.toList)
        (*
        let openCode args = 
            <@@ 
                let conn = new SqlConnection(connectionString) 
                conn.Open()
                conn 
            @@>
        let openMethod = ProvidedMethod("Open", [], typeof<SqlConnection>, IsStaticMethod = true, InvokeCode = openCode)

        ty.AddMembers([openMethod])
        *)
        //let ass = ProvidedAssembly(Path.ChangeExtension(Path.GetTempFileName(), ".dll"))
        //ass.AddTypes([ty])
        let assemblyFileName = Path.ChangeExtension(Path.GetTempFileName(), ".dll")
        (*
        let assemblyGenerator = AssemblyGenerator(assemblyFileName)
        let assemblyLazy = 
            lazy 
                printfn "--->assemblyLazy: %A" ty.FullName
                assemblyGenerator.Generate(ty)
                assemblyGenerator.Assembly
        *)
        printfn "--->this.Generate: %s" ty.FullName
        MicroORMAssembly.createAssembly(ty.FullName, connectionString, propStyle, assemblyFileName) |> ignore
        let assembly = Assembly.LoadFile assemblyFileName
        //ty.SetAssembly assembly
        //let ty = assembly.GetType(ty.FullName)
        ty

    let invalidateE = new Event<EventHandler,EventArgs>()    
    let providedNamespaces = ResizeArray()

    do //base.RegisterRuntimeAssemblyLocationAsProbingFolder(cfg)
       containerType.DefineStaticParameters(parameters, buildAssembly)
       //this.AddNamespace(ns, [containerType])
       providedNamespaces.Add(ns, [containerType])

    (*
        lazy [| for (namespaceName,types) in namespacesAndTypes do 
                     yield Local.makeProvidedNamespace namespaceName types 
                for (namespaceName,types) in otherNamespaces do 
                     yield Local.makeProvidedNamespace namespaceName types |]
                     *)
    interface ITypeProvider with
        [<CLIEvent>]
        override this.Invalidate = invalidateE.Publish

        override this.GetNamespaces() = providedNamespaces |> Seq.map (fun (ns, ts) -> Local.makeProvidedNamespace ns ts) |> Seq.toArray
        
        override this.GetInvokerExpression(syntheticMethodBase, parameters) = 
            printfn "---->GetInvokerExpression(%A, %A)" syntheticMethodBase parameters
            match syntheticMethodBase with
            | :? ConstructorInfo as ctor ->
                Expr.NewObject(ctor, Array.toList parameters) 
            | :? MethodInfo as mi ->
                if mi.IsStatic then 
                    Quotations.Expr.Call(mi, Array.toList parameters) 
                else
                    Quotations.Expr.Call(parameters.[0], mi, Array.toList parameters.[1..])
            | _ ->
                printfn "---->not implemented GetInvokerExpression(%A, %A)" syntheticMethodBase parameters
                NotImplementedException(sprintf "Not Implemented: GetInvokerExpression(%A, %A)" syntheticMethodBase parameters) |> raise 
                (*
        override __.GetStaticParameters ty = 
            match ty with
            | :? ProvidedTypeDefinition as t when ty.Name = t.Name ->
                let p1 = 
                    { new ParameterInfo() with
                        member z.Name = "ConnectionString"
                        member z.ParameterType = typeof<string>
                        member z.Attributes with get() = ParameterAttributes.Optional }
                [| p1 |]
            | _ -> Array.empty
        *)
        
        override this.GetStaticParameters(ty) =
            match ty with
            | :? ProvidedTypeDefinition as t when t.Name = "MicroORM" ->
                //if ty.Name = 
                //t.Name (* REVIEW: use equality? *) then
                    //parameters |> Seq.map (fun p -> p :> ParameterInfo) |> Seq.toArray
                    //let ps = t.GetStaticParameters()
                    let ps = parameters
                    printfn "--->%s static params: %A" ty.Name ps
                    ps |> Seq.map (fun p -> p :> ParameterInfo) |> Seq.toArray
                    (*
                    let p1 = 
                        { new ParameterInfo() with
                            member z.Name = "ConnectionString"
                            member z.ParameterType = typeof<string>
                            member z.Attributes with get() = ParameterAttributes.Optional }
                    [| p1 |]
                    *)
            | _ -> [| |]
            
        override this.ApplyStaticArguments(ty,typePathAfterArguments:string[],objs) = 
            let typePathAfterArguments = typePathAfterArguments.[typePathAfterArguments.Length-1]
            match ty with
            | :? ProvidedTypeDefinition as t -> (t.MakeParametricType(typePathAfterArguments,objs) :> Type)
            | _ -> failwith (sprintf "ApplyStaticArguments: static params for type %s are unexpected" ty.FullName)

        override x.GetGeneratedAssemblyContents(assembly:Assembly) = 
            //printfn "looking up assembly '%s'" assembly.FullName
            match GlobalProvidedAssemblyElementsTable.theTable.TryGetValue assembly with 
            | true,bytes -> bytes.Force()
            | _ -> 
                let bytes = System.IO.File.ReadAllBytes assembly.ManifestModule.FullyQualifiedName
                GlobalProvidedAssemblyElementsTable.theTable.[assembly] <- Lazy.CreateFromValue bytes
                bytes

    interface System.IDisposable with 
        member x.Dispose() = ()//AppDomain.CurrentDomain.remove_AssemblyResolve handler
*)