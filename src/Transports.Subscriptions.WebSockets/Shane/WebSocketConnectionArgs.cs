using System.Net.WebSockets;
using System.Threading;

namespace GraphQL.Server.Transports.WebSockets.Shane
{
    public record WebSocketConnectionArgs(WebSocket WebSocket, string ConnectionId, CancellationToken CancellationToken);
}
