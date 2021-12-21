using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GraphQL.Client.Http;

namespace GraphQL.Server.TestClient
{
    public class TestQueryResponse
    {
        public TestQueryObjectResponse Test { get; set; }
    }

    public class TestQueryObjectResponse
    {
        public int Value { get; set; }
    }
    public static class TestQueryHelper
    {
        public static async Task<IObservable<GraphQLResponse<TestQueryResponse>>> SubscriptionTestQuery(this GraphQLHttpClient client)
        {
            var subscriptionRequest = new GraphQLRequest
            {
                Query = @"
                    subscription  {
                        test {
                            value
                        }
                    }"
            };

            return client.CreateSubscriptionStream<TestQueryResponse>(subscriptionRequest);
        }
    }
}
