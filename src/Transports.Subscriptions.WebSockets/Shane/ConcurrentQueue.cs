#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GraphQL.Server.Transports.WebSockets.Shane
{
    internal class AsyncQueue<T>
        where T : class
    {
        private readonly Queue<T> _queue = new();
        private readonly Func<T, Task> _processData;
        private volatile bool _faulted;
        private readonly Func<Task> _returnDataAsyncDelegate;

        public AsyncQueue(Func<T, Task> processData)
        {
            _processData = processData ?? throw new ArgumentNullException(nameof(processData));
            _returnDataAsyncDelegate = ReturnDataAsync;
        }

        //queues the specified event and if necessary starts watching for an event to complete
        public void Enqueue(T queueData)
        {
            if (queueData == null)
                throw new ArgumentNullException(nameof(queueData));
            if (_faulted)
                throw new InvalidOperationException("Queue faulted");

            bool attach = false;
            lock (_queue)
            {
                _queue.Enqueue(queueData);
                attach = _queue.Count == 1;
            }
            //start watching for an event to complete, if this is the first in the queue
            if (attach)
            {
                //start returning data now
                Task.Run(_returnDataAsyncDelegate);
            }
        }

        //returns data from the queue in order (or raises errors or completed notifications)
        //executes until the queue is empty
        private async Task ReturnDataAsync()
        {
            T? queueData;
            lock (_queue)
            {
                queueData = _queue.Count > 0 ? _queue.Peek() : null;
            }
            while (queueData != null)
            {
                if (!_faulted)
                {
                    try
                    {
                        await _processData(queueData).ConfigureAwait(false);
                    }
                    catch
                    {
                        _faulted = true;
                    }
                }
                lock (_queue)
                {
                    _ = _queue.Dequeue();
                    queueData = _queue.Count > 0 ? _queue.Peek() : null;
                }
            }
        }
    }
}
