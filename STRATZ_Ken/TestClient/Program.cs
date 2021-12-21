using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.Server.TestClient;

namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            int startThreads = 100;
            var tasks = new Task[startThreads];
            foreach(var v in Enumerable.Range(0, startThreads))
            {
                tasks[v] = Task.Run(async () =>
                {
                    try
                    {
                        CancellationTokenSource subscriptionCancelToken = null;
                        GraphQLHttpClient GraphQLClient = null;
                        while (true)
                        {
                            if (subscriptionCancelToken == null || subscriptionCancelToken.Token.IsCancellationRequested)
                            {
                                Console.WriteLine("Connecting...");
                                GraphQLClient = new GraphQLHttpClient("http://testwebapp.dev1.acdmail.com/graphql", new NewtonsoftJsonSerializer());

                                subscriptionCancelToken = new CancellationTokenSource();
                                var subscriptionStream = await GraphQLClient.SubscriptionTestQuery();
                                subscriptionStream.Subscribe(response =>
                                {
                                    Console.WriteLine("Got data: " + response.Data.Test.Value);
                                }, onError =>
                                {
                                    subscriptionCancelToken.Cancel();
                                    Console.WriteLine("Connection to Pool has been lost. Reconnecting");
                                }, subscriptionCancelToken.Token);
                            }
                            await Task.Delay(1000);
                        }
                    } catch (Exception e)
                    {

                    }
                }, new CancellationToken());
            }

            Task.WhenAll(tasks).ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
