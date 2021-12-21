using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Types;

namespace GraphQL.Server.TestWebServer
{
    public class TestMutation : ObjectGraphType
    {
        public TestMutation()
        {
            Field<IntGraphType>()
                .Name("test")
                .Resolve((fieldContext) => {

                    return 64;
                });
        }
    }
}
