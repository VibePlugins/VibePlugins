using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestHarness
{
    /// <summary>
    /// TCP server that listens for a single test-host connection and exchanges
    /// length-prefixed JSON messages using <see cref="MessageSerializer"/>.
    /// Only one client is accepted at a time (one test host per server).
    /// </summary>
    public class HarnessTcpServer
    {
        private readonly int _port;
        private TcpListener _listener;
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Raised on the thread-pool when a complete message is received from the test host.
        /// </summary>
        public event System.Action<TestMessage> MessageReceived;

        /// <summary>
        /// Raised when a new test host client connects.
        /// </summary>
        public event System.Action ClientConnected;

        /// <summary>
        /// Initializes a new instance of <see cref="HarnessTcpServer"/>.
        /// </summary>
        /// <param name="port">The TCP port to listen on.</param>
        public HarnessTcpServer(int port)
        {
            _port = port;
        }

        /// <summary>
        /// Starts listening for TCP connections in the background.
        /// </summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        /// <summary>
        /// Stops the server and disconnects any active client.
        /// </summary>
        public void Stop()
        {
            _cts?.Cancel();

            try { _listener?.Stop(); } catch { /* swallow */ }
            try { _stream?.Close(); } catch { /* swallow */ }
            try { _client?.Close(); } catch { /* swallow */ }

            _listener = null;
            _stream = null;
            _client = null;
        }

        /// <summary>
        /// Sends a message to the currently connected test host.
        /// Thread-safe; writes are serialized via a semaphore.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>A task that completes when the message has been written.</returns>
        public async Task SendMessageAsync(TestMessage message)
        {
            var stream = _stream;
            if (stream == null)
            {
                return; // No client connected; silently drop.
            }

            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await MessageSerializer.WriteMessageAsync(stream, message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogException(ex, "[TestHarness] Error sending message");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Returns <c>true</c> if a test host client is currently connected.
        /// </summary>
        public bool IsClientConnected => _client?.Connected == true;

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);

                    // Disconnect any existing client before accepting the new one.
                    DisconnectCurrentClient();

                    _client = client;
                    _client.NoDelay = true;
                    _stream = _client.GetStream();

                    Rocket.Core.Logging.Logger.Log("[TestHarness] Test host connected.");

                    try { ClientConnected?.Invoke(); }
                    catch (Exception cbEx) { Rocket.Core.Logging.Logger.LogException(cbEx, "[TestHarness] ClientConnected callback error"); }

                    await ReceiveLoopAsync(_stream, ct).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    // Listener was stopped.
                    break;
                }
                catch (SocketException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        Rocket.Core.Logging.Logger.LogException(ex, "[TestHarness] Accept loop error");
                        await Task.Delay(1000, ct).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task ReceiveLoopAsync(NetworkStream stream, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var message = await MessageSerializer.ReadMessageAsync(stream, ct).ConfigureAwait(false);
                    MessageReceived?.Invoke(message);
                }
            }
            catch (EndOfStreamException)
            {
                Rocket.Core.Logging.Logger.Log("[TestHarness] Test host disconnected.");
            }
            catch (IOException)
            {
                Rocket.Core.Logging.Logger.Log("[TestHarness] Test host connection lost.");
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    Rocket.Core.Logging.Logger.LogException(ex, "[TestHarness] Receive loop error");
                }
            }
            finally
            {
                DisconnectCurrentClient();
            }
        }

        private void DisconnectCurrentClient()
        {
            try { _stream?.Close(); } catch { /* swallow */ }
            try { _client?.Close(); } catch { /* swallow */ }
            _stream = null;
            _client = null;
        }
    }
}
