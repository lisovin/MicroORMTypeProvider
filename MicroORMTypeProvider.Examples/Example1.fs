namespace MicroORMTypeProvider.Examples

open System

open MicroORMTypeProvider

type FooBar() = class end

type Db = MicroORM< @"Data Source=(localdb)\v11.0;Initial Catalog=wiztiles;Integrated Security=True">

