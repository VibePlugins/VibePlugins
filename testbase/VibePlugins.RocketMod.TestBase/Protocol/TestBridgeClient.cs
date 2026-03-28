using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestBase.Protocol
{
    /// <summary>
    /// TCP client that connects to the test harness running inside the Unturned server container.
    /// Provides request/response correlation and event-based message dispatch.
    /// </summary>
    public class TestBridgeClient : IAsyncDisposable
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveLoop;
        private bool _disposed;

        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<TestMessage>> _pendingRequests =
            new ConcurrentDictionary<Guid, TaskCompletionSource<TestMessage>>();

        private readonly ConcurrentDictionary<Type, TaskCompletionSource<TestMessage>> _typeWaiters =
            new ConcurrentDictionary<Type, TaskCompletionSource<TestMessage>>();

        private readonly object _sendLock = new object();

        /// <summary>
        /// Raised when an unsolicited message is received from the server
        /// (i.e. a message that does not match any pending request or type waiter).
        /// </summary>
        public event Action<TestMessage>? OnMessageReceived;

        /// <summary>
        /// Gets whether the client is currently connected to the harness.
        /// </summary>
        public bool IsConnected => _tcpClient?.Connected ?? false;

        /// <summary>
        /// Connects to the test harness TCP server at the specified host and port.
        /// </summary>
        /// <param name="host">The hostname or IP address of the harness.</param>
        /// <param name="port">The TCP port of the harness.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <exception cref="BridgeConnectionFailedException">If the connection cannot be established.</exception>
        public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host must not be null or empty.", nameof(host));

            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(host, port).ConfigureAwait(false);
                _stream = _tcpClient.GetStream();

                _receiveCts = new CancellationTokenSource();
                _receiveLoop = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);
            }
            catch (Exception ex) when (!(ex is BridgeConnectionFailedException))
            {
                throw new BridgeConnectionFailedException(
                    $"Failed to connect to test harness at {host}:{port}.", ex);
            }
        }

        /// <summary>
        /// Sends a message to the harness without waiting for a response.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task SendAsync(TestMessage message, CancellationToken ct = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            ThrowIfDisposed();

            byte[] data = MessageSerializer.Serialize(message);
            await _stream.WriteAsync(data, 0, data.Length, ct).ConfigureAwait(false);
            await _stream.FlushAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a request message and waits for a response with a matching <see cref="TestMessage.CorrelationId"/>.
        /// </summary>
        /// <typeparam name="TResponse">The expected response message type.</typeparam>
        /// <param name="request">The request message to send.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The response message cast to <typeparamref name="TResponse"/>.</returns>
        /// <exception cref="InvalidOperationException">If the response is not of the expected type.</exception>
        public async Task<TResponse> SendAndWaitAsync<TResponse>(TestMessage request, CancellationToken ct = default)
            where TResponse : TestMessage
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();

            var tcs = new TaskCompletionSource<TestMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (ct.Register(() => tcs.TrySetCanceled(ct)))
            {
                _pendingRequests[request.CorrelationId] = tcs;

                try
                {
                    await SendAsync(request, ct).ConfigureAwait(false);
                    TestMessage response = await tcs.Task.ConfigureAwait(false);

                    if (response is TResponse typed)
                        return typed;

                    throw new InvalidOperationException(
                        $"Expected response of type {typeof(TResponse).Name} but received {response.GetType().Name}.");
                }
                finally
                {
                    _pendingRequests.TryRemove(request.CorrelationId, out _);
                }
            }
        }

        /// <summary>
        /// Waits for the next message of the specified type from the receive stream.
        /// </summary>
        /// <typeparam name="T">The message type to wait for.</typeparam>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The received message cast to <typeparamref name="T"/>.</returns>
        public async Task<T> WaitForMessageAsync<T>(CancellationToken ct = default)
            where T : TestMessage
        {
            ThrowIfDisposed();

            var tcs = new TaskCompletionSource<TestMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (ct.Register(() => tcs.TrySetCanceled(ct)))
            {
                _typeWaiters[typeof(T)] = tcs;

                try
                {
                    TestMessage message = await tcs.Task.ConfigureAwait(false);
                    return (T)message;
                }
                finally
                {
                    _typeWaiters.TryRemove(typeof(T), out _);
                }
            }
        }

        /// <summary>
        /// Background loop that reads messages from the TCP stream and dispatches them
        /// to pending request waiters, type waiters, or the <see cref="OnMessageReceived"/> event.
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    TestMessage message = await MessageSerializer.ReadMessageAsync(_stream, ct).ConfigureAwait(false);

                    // First, check for a correlated request waiter
                    if (_pendingRequests.TryRemove(message.CorrelationId, out TaskCompletionSource<TestMessage> requestTcs))
                    {
                        requestTcs.TrySetResult(message);
                        continue;
                    }

                    // Second, check for a type-based waiter
                    Type messageType = message.GetType();
                    if (_typeWaiters.TryRemove(messageType, out TaskCompletionSource<TestMessage> typeTcs))
                    {
                        typeTcs.TrySetResult(message);
                        continue;
                    }

                    // Fall through: raise the unsolicited message event
                    try
                    {
                        OnMessageReceived?.Invoke(message);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(
                            $"[TestBridgeClient] OnMessageReceived handler threw: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                // Connection lost or protocol error — fail all pending waiters
                var exception = new BridgeConnectionFailedException(
                    "Bridge connection lost or protocol error.", ex);

                foreach (var kvp in _pendingRequests)
                {
                    if (_pendingRequests.TryRemove(kvp.Key, out TaskCompletionSource<TestMessage> tcs))
                        tcs.TrySetException(exception);
                }

                foreach (var kvp in _typeWaiters)
                {
                    if (_typeWaiters.TryRemove(kvp.Key, out TaskCompletionSource<TestMessage> tcs))
                        tcs.TrySetException(exception);
                }
            }
        }

        /// <summary>
        /// Disposes the TCP connection and cancels the background receive loop.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Cancel the receive loop
            if (_receiveCts != null)
            {
                _receiveCts.Cancel();

                if (_receiveLoop != null)
                {
                    try
                    {
                        await _receiveLoop.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                    catch (Exception)
                    {
                        // Swallow — loop already handled errors
                    }
                }

                _receiveCts.Dispose();
            }

            _stream?.Dispose();
            _tcpClient?.Dispose();

            // Fail any remaining waiters
            var disposed = new ObjectDisposedException(nameof(TestBridgeClient));
            foreach (var kvp in _pendingRequests)
            {
                if (_pendingRequests.TryRemove(kvp.Key, out TaskCompletionSource<TestMessage> tcs))
                    tcs.TrySetException(disposed);
            }

            foreach (var kvp in _typeWaiters)
            {
                if (_typeWaiters.TryRemove(kvp.Key, out TaskCompletionSource<TestMessage> tcs))
                    tcs.TrySetException(disposed);
            }
        }

        /// <summary>
        /// Throws <see cref="ObjectDisposedException"/> if the client has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TestBridgeClient));
        }
    }
}
