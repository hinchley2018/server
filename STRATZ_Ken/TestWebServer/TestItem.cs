using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Types;

namespace GraphQL.Server.TestWebServer
{
    public class TestItemType : ObjectGraphType<TestItem>
    {
        public TestItemType()
        {
            Field<IntGraphType>("Value", resolve: fieldContext => fieldContext.Source.Value);
        }
    }
    public class TestItem
    {
        public int Value { get; set; }
    }
}
