using System;
using VibePlugins.RocketMod.TestBase.Shared.Protocol;
using VibePlugins.RocketMod.TestHarness.Mocks;

namespace VibePlugins.RocketMod.TestHarness.Execution
{
    /// <summary>
    /// Thin facade that delegates to <see cref="Mocks.MockFactory"/> for backward
    /// compatibility with existing code that references this namespace.
    /// </summary>
    public static class MockFactory
    {
        /// <summary>
        /// Creates a mock entity based on the request and returns a handle.
        /// Delegates to <see cref="Mocks.MockFactory.Create"/>.
        /// </summary>
        public static CreateMockResponse Create(CreateMockRequest request)
        {
            return Mocks.MockFactory.Create(request);
        }

        /// <summary>
        /// Destroys all active mock entities.
        /// Delegates to <see cref="Mocks.MockFactory.DestroyAll"/>.
        /// </summary>
        public static void DestroyAll()
        {
            Mocks.MockFactory.DestroyAll();
        }
    }
}
