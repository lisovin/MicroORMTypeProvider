#r "System.Data.Entity.dll"
#r "FSharp.Data.TypeProviders.dll"
#r "System.Data.Linq.dll"

open System.Data.Linq
open System.Data.Entity
open Microsoft.FSharp.Data.TypeProviders

let [<Literal>] connectionString = @"Data Source=(localdb)\v11.0;Initial Catalog=wiztiles;Integrated Security=True"

type EntityConnection = SqlEntityConnection<ConnectionString=connectionString, Pluralize = true>

typeof<EntityConnection>.Assembly
 
typeof<EntityConnection.ServiceTypes.app>

