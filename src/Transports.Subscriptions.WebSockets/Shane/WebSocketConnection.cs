using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Server.Transports.Subscriptions.Abstractions;
using GraphQL.Server.Transports.WebSockets;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace GraphQL.Server.Transports.WebSockets.Shane
{
    public class WebSocketConnection<TSchema> : WebSocketConnection, IDisposable
        where TSchema : ISchema
    {
        private readonly WebSocket _socket;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ILogger _logger;
        private readonly string _connectionId;
        private readonly IGraphQLExecuter<TSchema> _executer;
        private readonly IDocumentWriter _documentWriter;
        private readonly CancellationToken _cancellationToken;
        private readonly CancellationToken _socketCancellationToken;
        private bool _disposed;
        private readonly AsyncQueue<Stream> _queue;

        public WebSocketConnection(
            WebSocketConnectionArgs args,
            IGraphQLExecuter<TSchema> executer,
            IDocumentWriter documentWriter,
            ILogger<WebSocketConnection> logger) : base(null, null)
        {
            _socket = args.WebSocket;
            _connectionId = args.ConnectionId;
            _logger = logger;
            _executer = executer;
            _documentWriter = documentWriter;
            _socketCancellationToken = args.CancellationToken;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_socketCancellationToken);
            _cancellationToken = _cancellationTokenSource.Token;
            _queue = new AsyncQueue<Stream>(WriteToWebSocketInternalAsync);
        }

        public override Task Connect() => ReadFromWebSocket();

        private async Task ReadFromWebSocket()
        {
            try
            {
                var buffer = WebSocket.CreateServerBuffer(4096);
                var bufferStream = new MemoryStream();
                while (true)
                {
                    var ret = await _socket.ReceiveAsync(buffer, _cancellationToken).ConfigureAwait(false);
                    if (ret.CloseStatus.HasValue)
                    {
                        if (ret.CloseStatus == WebSocketCloseStatus.NormalClosure)
                        {
                            //should block while cancelling any pending write operations
                            _cancellationTokenSource.Cancel();
                            //perform websocket close packets
                            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, _socketCancellationToken).ConfigureAwait(false);
                            return;
                        }
                        else
                            return;
                    }
                    var slicedBuffer = buffer.Slice(0, ret.Count);
                    bufferStream.Write(slicedBuffer);
                    if (ret.EndOfMessage)
                    {
                        //clone the variable before it is rewritten by the current thread
                        var dataStream = bufferStream;
                        _ = Task.Run(() => ProcessReceivedData(dataStream));
                        bufferStream = new MemoryStream();
                    }
                }
            }
            finally
            {
                _cancellationTokenSource.Cancel();
            }
        }

        private Task ProcessReceivedData(Stream stream)
        {
            try
            {
                var textReader = new StreamReader(stream, Encoding.UTF8);
                var reader = new JsonTextReader(textReader);
                var serializerSettings = new JsonSerializerSettings
                {
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    DateFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFF'Z'",
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                var serializer = JsonSerializer.Create(serializerSettings);
                var operationMessage = serializer.Deserialize<OperationMessage>(reader);

                return operationMessage.Type switch
                {
                    MessageType.GQL_CONNECTION_INIT => HandleInitAsync(operationMessage),
                    MessageType.GQL_START => HandleStartAsync(operationMessage),
                    MessageType.GQL_STOP => HandleStopAsync(operationMessage),
                    MessageType.GQL_CONNECTION_TERMINATE => HandleTerminateAsync(operationMessage),
                    _ => HandleUnknownAsync(operationMessage),
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ProcessReceivedData: {ex}");
                return Task.CompletedTask;
            }
        }

        //=================================
        private Task HandleUnknownAsync(OperationMessage message)
        {
            _logger.LogError($"Unexpected message type: {message.Type}");
            return WriteToWebSocket(new OperationMessage
            {
                Type = MessageType.GQL_CONNECTION_ERROR,
                Id = message.Id,
                Payload = new ExecutionResult
                {
                    Errors = new ExecutionErrors
                    {
                        new ExecutionError($"Unexpected message type {message.Type}")
                    }
                }
            });
        }

        private Task HandleStopAsync(OperationMessage message)
        {
            _logger.LogDebug("Handle stop: {id}", message.Id);
            return UnsubscribeAsync(message.Id);
        }

        private Task HandleStartAsync(OperationMessage message)
        {
            _logger.LogDebug("Handle start: {id}", message.Id);
            var payload = ((JObject)message.Payload).ToObject<OperationMessagePayload>();
            if (payload == null)
                throw new InvalidOperationException("Could not get OperationMessagePayload from message.Payload");

            return SubscribeOrExecuteAsync(message.Id, payload);
        }

        private Task HandleInitAsync(OperationMessage message)
        {
            _logger.LogDebug("Handle init");
            return WriteToWebSocket(new OperationMessage
            {
                Type = MessageType.GQL_CONNECTION_ACK
            });
        }

        private Task HandleTerminateAsync(OperationMessage message)
        {
            _logger.LogDebug("Handle terminate");
            return context.Terminate();
        }

        //=================================

        private async Task Execute()
        {

        }

        private async Task WriteToWebSocket(OperationMessage message)
        {
            MemoryStream data = new();
            await _documentWriter.WriteAsync(data, message, _cancellationToken);
            data.Position = 0;
            if (data.Length > 0)
                _queue.Enqueue(data);
        }

        /// <summary>
        /// Executed in an orderly fashion when data is queued via _queue.Enqueue
        /// </summary>
        private async Task WriteToWebSocketInternalAsync(Stream data)
        {
            try
            {
                var buffer = WebSocket.CreateServerBuffer(4096);
                bool isEnd = false;
                do
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    var bytesRead = data.Read(buffer);
                    isEnd = data.Position < data.Length;
                    var slicedBuffer = buffer.Slice(0, bytesRead);
                    await _socket.SendAsync(slicedBuffer, WebSocketMessageType.Binary, isEnd, _cancellationToken).ConfigureAwait(false);
                }
                while (!isEnd);
            }
            catch
            {
                _cancellationTokenSource.Cancel();
                throw;
            }
        }

        public virtual void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }
    }
}
