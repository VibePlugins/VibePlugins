using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SDG.Unturned;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;

namespace VibePlugins.RocketMod.TestHarness.Execution
{
    /// <summary>
    /// Routes incoming <see cref="TestMessage"/> instances to the appropriate handler
    /// based on <see cref="TestMessageType"/>. All game-API calls are dispatched to the
    /// Unity main thread via <see cref="MainThreadDispatcher"/>.
    /// </summary>
    public class TestCommandDispatcher
    {
        private readonly HarnessTcpServer _server;
        private readonly Dictionary<TestMessageType, Func<TestMessage, Task>> _handlers;

        /// <summary>
        /// Registered cleanup callbacks that are executed on <see cref="TestMessageType.RunCleanup"/>.
        /// </summary>
        private readonly List<Func<Task>> _cleanupCallbacks = new List<Func<Task>>();

        /// <summary>
        /// Initializes a new instance of <see cref="TestCommandDispatcher"/>.
        /// </summary>
        /// <param name="server">The TCP server used to send responses.</param>
        public TestCommandDispatcher(HarnessTcpServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));

            _handlers = new Dictionary<TestMessageType, Func<TestMessage, Task>>
            {
                { TestMessageType.ExecuteCommand, HandleExecuteCommandAsync },
                { TestMessageType.RunCode, HandleRunCodeAsync },
                { TestMessageType.CreateMock, HandleCreateMockAsync },
                { TestMessageType.WaitTicks, HandleWaitTicksAsync },
                { TestMessageType.MonitorEvent, HandleMonitorEventAsync },
                { TestMessageType.WaitForEvent, HandleWaitForEventAsync },
                { TestMessageType.RunCleanup, HandleRunCleanupAsync },
                { TestMessageType.DestroyAllMocks, HandleDestroyAllMocksAsync },
                { TestMessageType.ClearEventCaptures, HandleClearEventCapturesAsync },
                { TestMessageType.Heartbeat, HandleHeartbeatAsync },
                { TestMessageType.Shutdown, HandleShutdownAsync },
            };
        }

        /// <summary>
        /// Registers a cleanup callback that will be invoked when a
        /// <see cref="RunCleanupRequest"/> is received.
        /// </summary>
        /// <param name="callback">The async cleanup action.</param>
        public void RegisterCleanup(Func<Task> callback)
        {
            if (callback != null)
            {
                _cleanupCallbacks.Add(callback);
            }
        }

        /// <summary>
        /// Dispatches a received message to its corresponding handler.
        /// </summary>
        /// <param name="message">The message to dispatch.</param>
        public async Task DispatchAsync(TestMessage message)
        {
            if (message == null) return;

            if (_handlers.TryGetValue(message.MessageType, out var handler))
            {
                await handler(message).ConfigureAwait(false);
            }
            else
            {
                Rocket.Core.Logging.Logger.LogWarning(
                    $"[TestHarness] No handler for message type: {message.MessageType}");
            }
        }

        // ── ExecuteCommand ──────────────────────────────────────────

        private async Task HandleExecuteCommandAsync(TestMessage msg)
        {
            var request = (ExecuteCommandRequest)msg;
            var response = new ExecuteCommandResponse { CorrelationId = request.CorrelationId };

            try
            {
                response = await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    return CommandExecutor.Execute(request);
                }).ConfigureAwait(false);

                response.CorrelationId = request.CorrelationId;
            }
            catch (Exception ex)
            {
                response.Status = CommandStatus.Exception;
                response.ExceptionInfo = SerializableExceptionInfo.FromException(ex);
            }

            await _server.SendMessageAsync(response).ConfigureAwait(false);
        }

        // ── RunCode ─────────────────────────────────────────────────

        private async Task HandleRunCodeAsync(TestMessage msg)
        {
            var request = (RunCodeRequest)msg;
            RunCodeResponse response;

            try
            {
                response = await RemoteInvoker.InvokeAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                response = new RunCodeResponse
                {
                    ExceptionInfo = SerializableExceptionInfo.FromException(ex)
                };
            }

            response.CorrelationId = request.CorrelationId;
            await _server.SendMessageAsync(response).ConfigureAwait(false);
        }

        // ── CreateMock ──────────────────────────────────────────────

        private async Task HandleCreateMockAsync(TestMessage msg)
        {
            var request = (CreateMockRequest)msg;
            CreateMockResponse response;

            try
            {
                response = await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    return MockFactory.Create(request);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                response = new CreateMockResponse
                {
                    Success = false,
                    Error = ex.ToString()
                };
            }

            response.CorrelationId = request.CorrelationId;
            await _server.SendMessageAsync(response).ConfigureAwait(false);
        }

        // ── WaitTicks ───────────────────────────────────────────────

        private async Task HandleWaitTicksAsync(TestMessage msg)
        {
            var request = (WaitTicksRequest)msg;

            try
            {
                await TickAwaiter.WaitAsync(request.Count, request.TickType).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogException(ex, "[TestHarness] WaitTicks error");
            }

            var response = new WaitTicksResponse { CorrelationId = request.CorrelationId };
            await _server.SendMessageAsync(response).ConfigureAwait(false);
        }

        // ── MonitorEvent ────────────────────────────────────────────

        private async Task HandleMonitorEventAsync(TestMessage msg)
        {
            var request = (MonitorEventRequest)msg;

            EventCapture.StartMonitor(request.MonitorId, request.EventTypeName);

            // No explicit response message; monitoring is fire-and-forget.
            // Events will be sent as EventOccurredResponse when they happen.
            await Task.CompletedTask;
        }

        // ── WaitForEvent ────────────────────────────────────────────

        private async Task HandleWaitForEventAsync(TestMessage msg)
        {
            var request = (WaitForEventRequest)msg;

            EventOccurredResponse response;

            try
            {
                response = await EventCapture.WaitForAsync(request).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                response = new EventOccurredResponse
                {
                    MonitorId = request.MonitorId,
                    SerializedEvent = null
                };
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogException(ex, "[TestHarness] WaitForEvent error");
                response = new EventOccurredResponse
                {
                    MonitorId = request.MonitorId,
                    SerializedEvent = null
                };
            }

            response.CorrelationId = request.CorrelationId;
            await _server.SendMessageAsync(response).ConfigureAwait(false);
        }

        // ── RunCleanup ──────────────────────────────────────────────

        private async Task HandleRunCleanupAsync(TestMessage msg)
        {
            var request = (RunCleanupRequest)msg;
            var response = new RunCleanupResponse { CorrelationId = request.CorrelationId };

            try
            {
                foreach (var callback in _cleanupCallbacks)
                {
                    await callback().ConfigureAwait(false);
                }

                response.Success = true;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Error = ex.ToString();
            }

            _cleanupCallbacks.Clear();
            await _server.SendMessageAsync(response).ConfigureAwait(false);
        }

        // ── DestroyAllMocks ─────────────────────────────────────────

        private async Task HandleDestroyAllMocksAsync(TestMessage msg)
        {
            var request = (DestroyAllMocksRequest)msg;
            var response = new DestroyAllMocksResponse { CorrelationId = request.CorrelationId };

            try
            {
                await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    MockFactory.DestroyAll();
                    return Task.CompletedTask;
                }).ConfigureAwait(false);

                response.Success = true;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Error = ex.ToString();
            }

            await _server.SendMessageAsync(response).ConfigureAwait(false);
        }

        // ── ClearEventCaptures ──────────────────────────────────────

        private async Task HandleClearEventCapturesAsync(TestMessage msg)
        {
            var request = (ClearEventCapturesRequest)msg;

            EventCapture.ClearAll();

            var response = new ClearEventCapturesResponse
            {
                CorrelationId = request.CorrelationId,
                Success = true
            };

            await _server.SendMessageAsync(response).ConfigureAwait(false);
        }

        // ── Heartbeat ───────────────────────────────────────────────

        private async Task HandleHeartbeatAsync(TestMessage msg)
        {
            var response = new HeartbeatMessage
            {
                CorrelationId = msg.CorrelationId
            };

            await _server.SendMessageAsync(response).ConfigureAwait(false);
        }

        // ── Shutdown ────────────────────────────────────────────────

        private async Task HandleShutdownAsync(TestMessage msg)
        {
            Rocket.Core.Logging.Logger.Log("[TestHarness] Shutdown requested by test host.");

            await MainThreadDispatcher.EnqueueAsync(() =>
            {
                Provider.shutdown();
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
    }
}
