namespace VibePlugins.RocketMod.TestBase.Containers
{
    /// <summary>
    /// Configuration options for the MySQL sidecar container used alongside the Unturned server.
    /// Supports a fluent builder pattern for convenient construction.
    /// </summary>
    public class MySqlSidecarOptions
    {
        /// <summary>The database name to create. Defaults to <c>"test"</c>.</summary>
        public string Database { get; set; } = "test";

        /// <summary>The MySQL root username. Defaults to <c>"root"</c>.</summary>
        public string Username { get; set; } = "root";

        /// <summary>The MySQL root password. Defaults to <c>"test"</c>.</summary>
        public string Password { get; set; } = "test";

        /// <summary>The port to expose for MySQL connections. Defaults to <c>3306</c>.</summary>
        public int Port { get; set; } = 3306;

        /// <summary>Sets the database name.</summary>
        /// <param name="database">The database name to create.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public MySqlSidecarOptions WithDatabase(string database)
        {
            Database = database;
            return this;
        }

        /// <summary>Sets the MySQL username.</summary>
        /// <param name="username">The username.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public MySqlSidecarOptions WithUsername(string username)
        {
            Username = username;
            return this;
        }

        /// <summary>Sets the MySQL password.</summary>
        /// <param name="password">The password.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public MySqlSidecarOptions WithPassword(string password)
        {
            Password = password;
            return this;
        }

        /// <summary>Sets the MySQL port.</summary>
        /// <param name="port">The port number.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public MySqlSidecarOptions WithPort(int port)
        {
            Port = port;
            return this;
        }
    }
}
