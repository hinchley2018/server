using System;
using System.Threading.Tasks;
using GraphQL.Server.Transports.Subscriptions.Abstractions;

namespace GraphQL.Server.Transports.WebSockets
{
    public class WebSocketConnection : IDisposable
    {
        private readonly WebSocketTransport _transport;
        private readonly SubscriptionServer _server;

        public WebSocketConnection(
            WebSocketTransport transport,
            SubscriptionServer subscriptionServer)
        {
            this.MyLog("Constructor");
            _transport = transport;
            _server = subscriptionServer;
        }

        public virtual async Task Connect()
        {
            this.MyLog("OnConnect");
            await _server.OnConnect();
            this.MyLog("OnDisconnect");
            await _server.OnDisconnect();
            this.MyLog("CloseAsync");
            await _transport.CloseAsync();
            this.MyLog("Connect finished");
        }

        public virtual void Dispose()
        {
            this.MyLog("Dispose");
            _server.Dispose();
            _transport.Dispose();
            this.MyLog("Dispose finished");
        }
    }
}
