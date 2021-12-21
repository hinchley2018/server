using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;

namespace GraphQL.Server.TestWebServer
{
    public class TestSchema : Schema
    {
        public TestSchema(IServiceProvider provider) : base(provider)
        {
            Query = provider.GetRequiredService<TestQuery>();
            Mutation = provider.GetRequiredService<TestMutation>();
            Subscription = provider.GetRequiredService<TestSubscription>();
        }
    }
}
