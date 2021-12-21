using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.MicrosoftDI;
using GraphQL.Types;

namespace GraphQL.Server.TestWebServer
{
    public class TestQuery : ObjectGraphType
    {
        public TestQuery()
        {
            Field<IntGraphType>()
                .Name("test")
                .Resolve((fieldContext) => {

                    return 54;
                });
        }
    }
}
