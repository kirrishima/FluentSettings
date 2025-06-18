using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestWINUIApp
{
    internal class MyAttribute : Attribute
    {
        public MyAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    namespace A.B.C.D.E.F.Gay
    {
        public class MyClass
        {

        }
    }
}
