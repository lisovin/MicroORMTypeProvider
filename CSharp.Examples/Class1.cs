using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MicroORMTypeProvider.Examples;

namespace CSharp.Examples
{
    public class Class1
    {
        public void Test()
        {
            var myapp = new Db.app();
            myapp.AppId = 1;
        }

    }
}
