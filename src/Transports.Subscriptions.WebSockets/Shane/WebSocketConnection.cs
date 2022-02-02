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
        private readonly AsyncQueue<Stream> _outputQueue;
        private readonly AsyncQueue<Stream> _inputQueue;
        private readonly Dictionary<string, IDisposable> _subscriptions;
        private string? _closeError = null;
        private int _statusInt;

        private Status _status
        {
            get => (Status)_statusInt;
            set => _statusInt = (int)value;
        }

        private Status CompareExchangeStatus(Status value, Status comparand)
            => (Status)Interlocked.CompareExchange(ref _statusInt, (int)value, (int)comparand);

        private enum Status
        {
            Init = 0,
            Connected = 1,
            Closing = 2,
            Closed = 3,
        }

        public WebSocketConnection(
            WebSocketConnectionArgs args,
            IGraphQLExecuter<TSchema> executer,
            IDocumentWriter documentWriter,
            ILogger<WebSocketConnection> logger) : base(null, null)
        {
            _status = Status.Init;
            _socket = args.WebSocket;
            _connectionId = args.ConnectionId;
            _logger = logger;
            _executer = executer;
            _documentWriter = documentWriter;
            _socketCancellationToken = args.CancellationToken;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_socketCancellationToken);
            _cancellationToken = _cancellationTokenSource.Token;
            _outputQueue = new AsyncQueue<Stream>(WriteToWebSocketInternalAsync);
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
                            // finish writing pending data blocks, then send close request
                            WriteToWebSocketClose();
                        else
                            // terminate immediately
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
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _closeError ??= "Unknown error";
                _logger.LogError($"Unexpected error in ReadFromWebsocket: {ex}");
            }
            _cancellationTokenSource.Cancel();
            if (_closeError != null)
                _logger.LogInformation($"Abnormal websocket closure: {_closeError}");
            try
            {
                if (!_socketCancellationToken.IsCancellationRequested)
                {
                    //todo: wait for pending messages to finish sending
                    await _socket.CloseAsync(_closeError == null ? WebSocketCloseStatus.NormalClosure : WebSocketCloseStatus.ProtocolError, _closeError, _socketCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error in ReadFromWebsocket: {ex}");
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
            WriteToWebSocketClose();
            return Task.CompletedTask;
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
                _outputQueue.Enqueue(data);
        }

        private Task WriteToWebSocketKeepAlive()
            => WriteToWebSocket(new OperationMessage { Type = MessageType.GQL_CONNECTION_KEEP_ALIVE });

        private void WriteToWebSocketClose()
        {
            _outputQueue.Enqueue(new MemoryStream());
        }

        /// <summary>
        /// Executed in an orderly fashion when data is queued via _queue.Enqueue
        /// </summary>
        private async Task WriteToWebSocketInternalAsync(Stream data)
        {
            try
            {
                if (data.Length == 0)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, _cancellationToken);
                    _cancellationTokenSource.Cancel();
                }
                else
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
