using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VibePlugins.RocketMod.TestBase.Protocol;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using Xunit.Sdk;

namespace VibePlugins.RocketMod.TestBase.Assertions
{
    /// <summary>
    /// A builder for defining test scenarios with setup steps, actions, and expectations.
    /// Scenarios are executed in order: all <c>Given</c> steps run first, then <c>When</c>
    /// actions, followed by <c>ThenExpect</c> assertions.
    /// </summary>
    /// <remarks>
    /// Usage example:
    /// <code>
    /// await CreateScenario()
    ///     .Given(async () => await CreatePlayerAsync(p => p.Name = "Alice"))
    ///     .When(async () => await ExecuteCommand("heal").WithArgs("Alice").RunAsync())
    ///     .ThenExpectMessage("healed")
    ///     .ThenExpectNoEvent&lt;PlayerDeathEvent&gt;()
    ///     .ExecuteAsync()
    ///     .ShouldPass();
    /// </code>
    /// </remarks>
    public class ScenarioBuilder
    {
        private readonly TestBridgeClient _bridge;
        private readonly List<Func<Task>> _givenSteps = new List<Func<Task>>();
        private readonly List<Func<Task>> _whenActions = new List<Func<Task>>();
        private readonly List<ExpectationEntry> _expectations = new List<ExpectationEntry>();

        /// <summary>
        /// Initializes a new <see cref="ScenarioBuilder"/> backed by the specified bridge client.
        /// </summary>
        /// <param name="bridge">The connected bridge client.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="bridge"/> is <c>null</c>.</exception>
        internal ScenarioBuilder(TestBridgeClient bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        /// <summary>
        /// Adds a setup step that runs before the main action. Multiple <c>Given</c> steps
        /// run in the order they were added.
        /// </summary>
        /// <param name="setup">An async function performing setup work.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public ScenarioBuilder Given(Func<Task> setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            _givenSteps.Add(setup);
            return this;
        }

        /// <summary>
        /// Adds the main action under test. Multiple <c>When</c> actions run in the order
        /// they were added, after all <c>Given</c> steps.
        /// </summary>
        /// <param name="action">An async function performing the action under test.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public ScenarioBuilder When(Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _whenActions.Add(action);
            return this;
        }

        /// <summary>
        /// Adds an expectation that an event of type <typeparamref name="TEvent"/> will occur
        /// after the <c>When</c> actions, optionally matching a predicate.
        /// </summary>
        /// <typeparam name="TEvent">The expected event type.</typeparam>
        /// <param name="predicate">
        /// An optional predicate to match against the event. If <c>null</c>, any event of the type matches.
        /// </param>
        /// <param name="timeout">
        /// Maximum time to wait for the event. Defaults to 10 seconds.
        /// </param>
        /// <returns>This builder for fluent chaining.</returns>
        public ScenarioBuilder ThenExpect<TEvent>(
            Func<TEvent, bool> predicate = null,
            TimeSpan? timeout = null) where TEvent : class
        {
            _expectations.Add(new EventExpectation<TEvent>(
                _bridge, predicate, timeout ?? TimeSpan.FromSeconds(10), expectOccurrence: true));
            return this;
        }

        /// <summary>
        /// Adds an expectation that at least one chat message containing the specified substring
        /// will be produced by the <c>When</c> actions.
        /// </summary>
        /// <param name="substring">The text to search for in chat messages.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public ScenarioBuilder ThenExpectMessage(string substring)
        {
            if (substring == null) throw new ArgumentNullException(nameof(substring));
            _expectations.Add(new ChatMessageExpectation(_bridge, substring));
            return this;
        }

        /// <summary>
        /// Adds an expectation that no event of type <typeparamref name="TEvent"/> occurs
        /// within the given timeout after the <c>When</c> actions.
        /// </summary>
        /// <typeparam name="TEvent">The event type that should not occur.</typeparam>
        /// <param name="timeout">
        /// How long to wait before concluding no event occurred. Defaults to 2 seconds.
        /// </param>
        /// <returns>This builder for fluent chaining.</returns>
        public ScenarioBuilder ThenExpectNoEvent<TEvent>(TimeSpan? timeout = null) where TEvent : class
        {
            _expectations.Add(new EventExpectation<TEvent>(
                _bridge, predicate: null, timeout ?? TimeSpan.FromSeconds(2), expectOccurrence: false));
            return this;
        }

        /// <summary>
        /// Executes the scenario: runs all <c>Given</c> steps, then <c>When</c> actions,
        /// then evaluates all expectations.
        /// </summary>
        /// <returns>
        /// A <see cref="ScenarioResult"/> summarising which expectations passed or failed.
        /// </returns>
        public async Task<ScenarioResult> ExecuteAsync()
        {
            var failures = new List<string>();

            // 1. Given
            foreach (Func<Task> setup in _givenSteps)
            {
                try
                {
                    await setup().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    failures.Add($"Given step failed: {ex.Message}");
                    return new ScenarioResult(failures);
                }
            }

            // 2. When
            foreach (Func<Task> action in _whenActions)
            {
                try
                {
                    await action().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    failures.Add($"When action failed: {ex.Message}");
                    return new ScenarioResult(failures);
                }
            }

            // 3. Then
            foreach (ExpectationEntry expectation in _expectations)
            {
                try
                {
                    await expectation.EvaluateAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    failures.Add(ex.Message);
                }
            }

            return new ScenarioResult(failures);
        }

        // ─────────────────────────────────────────────────────────────
        // Internal expectation types
        // ─────────────────────────────────────────────────────────────

        /// <summary>Base class for scenario expectations.</summary>
        private abstract class ExpectationEntry
        {
            public abstract Task EvaluateAsync();
        }

        /// <summary>Expectation that an event does or does not occur.</summary>
        private sealed class EventExpectation<TEvent> : ExpectationEntry where TEvent : class
        {
            private readonly TestBridgeClient _bridge;
            private readonly Func<TEvent, bool> _predicate;
            private readonly TimeSpan _timeout;
            private readonly bool _expectOccurrence;

            public EventExpectation(
                TestBridgeClient bridge,
                Func<TEvent, bool> predicate,
                TimeSpan timeout,
                bool expectOccurrence)
            {
                _bridge = bridge;
                _predicate = predicate;
                _timeout = timeout;
                _expectOccurrence = expectOccurrence;
            }

            public override async Task EvaluateAsync()
            {
                // Start a temporary monitor for the check.
                EventMonitor<TEvent> monitor = await _bridge
                    .MonitorEventAsync<TEvent>()
                    .ConfigureAwait(false);

                if (_expectOccurrence)
                {
                    // Should occur.
                    try
                    {
                        await monitor.WaitForAsync(_predicate, _timeout).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        throw new XunitException(
                            $"Expected event '{typeof(TEvent).Name}' did not occur within " +
                            $"{_timeout.TotalSeconds:F1}s.");
                    }
                }
                else
                {
                    // Should not occur.
                    await monitor.ShouldNotOccurAsync(_predicate, _timeout).ConfigureAwait(false);
                }
            }
        }

        /// <summary>Expectation that a chat message containing a substring is produced.</summary>
        private sealed class ChatMessageExpectation : ExpectationEntry
        {
            private readonly TestBridgeClient _bridge;
            private readonly string _substring;

            public ChatMessageExpectation(TestBridgeClient bridge, string substring)
            {
                _bridge = bridge;
                _substring = substring;
            }

            public override async Task EvaluateAsync()
            {
                EventMonitor<ChatMessageEvent> monitor = await _bridge
                    .MonitorEventAsync<ChatMessageEvent>()
                    .ConfigureAwait(false);

                try
                {
                    await monitor.WaitForAsync(
                        e => e.Text != null && e.Text.Contains(_substring),
                        TimeSpan.FromSeconds(10))
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    throw new XunitException(
                        $"Expected a chat message containing '{_substring}', but none was received " +
                        "within 10s.");
                }
            }
        }
    }

    /// <summary>
    /// The result of executing a <see cref="ScenarioBuilder"/>, summarising which expectations
    /// passed and which failed.
    /// </summary>
    public class ScenarioResult
    {
        private readonly List<string> _failures;

        /// <summary>
        /// Initializes a new <see cref="ScenarioResult"/>.
        /// </summary>
        /// <param name="failures">The list of failure messages. Empty if all expectations passed.</param>
        internal ScenarioResult(List<string> failures)
        {
            _failures = failures ?? new List<string>();
        }

        /// <summary>Gets whether all expectations were met.</summary>
        public bool AllExpectationsMet => _failures.Count == 0;

        /// <summary>Gets the list of failure descriptions, if any.</summary>
        public IReadOnlyList<string> Failures => _failures.AsReadOnly();

        /// <summary>
        /// Throws <see cref="XunitException"/> if any expectation failed, listing all failures.
        /// </summary>
        /// <exception cref="XunitException">If one or more expectations were not met.</exception>
        public void ShouldPass()
        {
            if (_failures.Count > 0)
            {
                throw new XunitException(
                    $"Scenario failed with {_failures.Count} expectation(s) not met:\n" +
                    string.Join("\n", _failures));
            }
        }
    }
}
