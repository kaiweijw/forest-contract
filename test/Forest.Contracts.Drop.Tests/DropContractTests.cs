using System;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest.Contracts.Drop
{
    public class DropContractTests : DropContractTestBase
    {
        
        [Fact]
        private void TestFrameworkAttribute()
        {
            string indexed = "CiIKIOa4WICUIZODb9cbe2+FuxItjMzAO2i4GEwMkaTFPM/XEiIKIK183aoU/ISU/lSr9RxiWJK9sdyu5w77MkgOkmwHjynVGnAKbgoMV0pCQVRDSC0yNjMxEAEaDFdKQkFUQ0gtMjYzMSCY9XEqSmh0dHBzOi8vZm9yZXN0LWRldi5zMy5hbWF6b25hd3MuY29tL0ZvcmVzdC10ZXN0NC9kcm9wXzE3MDY5NDExMzU4NjNfMS5qcGVnIAEoAg==";
            var transferInput = DropClaimAdded.Parser.ParseFrom(Convert.FromBase64String(indexed));
            Console.WriteLine(transferInput);
        }
    }
}