using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Resolvers;
using GraphQL.Types;

namespace GraphQL.Server.TestWebServer
{
    public class TestSubscription : ObjectGraphType
    {
        public TestSubscription(System.IServiceProvider serviceProvider,
            TestService testService)
        {
            AddField(new EventStreamFieldType
            {
                Name = "test",
                Type = typeof(TestItemType),
                Resolver = new FuncFieldResolver<TestItem>(fieldContext => fieldContext.Source as TestItem),
                Subscriber = new EventStreamResolver<TestItem>(fieldContext => testService.GetEvents())
            });
        }
    }
}
