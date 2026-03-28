using System;

namespace VibePlugins.RocketMod.TestBase
{
    /// <summary>
    /// Thrown when a plugin fails to load inside the Unturned server container.
    /// </summary>
    public class PluginLoadFailedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of <see cref="PluginLoadFailedException"/>.
        /// </summary>
        public PluginLoadFailedException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="PluginLoadFailedException"/> with a message.
        /// </summary>
        /// <param name="message">The error message.</param>
        public PluginLoadFailedException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="PluginLoadFailedException"/> with a message and inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public PluginLoadFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Thrown when the Unturned server container fails to start or become ready.
    /// </summary>
    public class ServerStartupFailedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ServerStartupFailedException"/>.
        /// </summary>
        public ServerStartupFailedException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ServerStartupFailedException"/> with a message.
        /// </summary>
        /// <param name="message">The error message.</param>
        public ServerStartupFailedException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ServerStartupFailedException"/> with a message and inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ServerStartupFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Thrown when the test bridge TCP client fails to connect to the harness inside the container.
    /// </summary>
    public class BridgeConnectionFailedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of <see cref="BridgeConnectionFailedException"/>.
        /// </summary>
        public BridgeConnectionFailedException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="BridgeConnectionFailedException"/> with a message.
        /// </summary>
        /// <param name="message">The error message.</param>
        public BridgeConnectionFailedException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="BridgeConnectionFailedException"/> with a message and inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public BridgeConnectionFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Thrown when a remote code execution request on the server fails.
    /// </summary>
    public class RemoteExecutionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RemoteExecutionException"/>.
        /// </summary>
        public RemoteExecutionException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RemoteExecutionException"/> with a message.
        /// </summary>
        /// <param name="message">The error message.</param>
        public RemoteExecutionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RemoteExecutionException"/> with a message and inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public RemoteExecutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
