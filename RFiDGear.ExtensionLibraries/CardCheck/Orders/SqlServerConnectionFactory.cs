using System.Data.Common;
using System.Data.SqlClient;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>Makes the connection the store uses; a test replaces it with a connection to another engine.</summary>
    internal interface IDbConnectionFactory
    {
        /// <summary>Creates a new, closed connection.</summary>
        /// <returns>The connection; the caller disposes it.</returns>
        DbConnection Create();
    }

    /// <summary>
    /// The connection to the order database: SQL Server, the Windows account of the person running the check. There
    /// is no user name or password anywhere; the settings hold only the server and the database.
    /// </summary>
    internal sealed class SqlServerConnectionFactory : IDbConnectionFactory
    {
        private readonly DatabaseSettings _settings;

        /// <summary>Creates the factory.</summary>
        /// <param name="settings">Where the order database is.</param>
        public SqlServerConnectionFactory(DatabaseSettings settings)
        {
            _settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
        }

        /// <inheritdoc />
        public DbConnection Create() => new SqlConnection(BuildConnectionString(_settings));

        /// <summary>
        /// The connection string. It keeps what the old assistant used (integrated security, trusting the server's
        /// certificate, two retries ten seconds apart, a thirty second connect timeout) and, as before, names the
        /// instance as the application, but only when the server name has an instance.
        /// </summary>
        /// <param name="settings">Where the order database is.</param>
        /// <returns>The connection string.</returns>
        internal static string BuildConnectionString(DatabaseSettings settings)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = settings.Server,
                InitialCatalog = settings.Database,
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                ConnectRetryCount = 2,
                ConnectRetryInterval = 10,
                ConnectTimeout = 30,
            };

            var slash = settings.Server.IndexOf('\\');

            if (slash >= 0 && slash + 1 < settings.Server.Length)
            {
                builder.ApplicationName = settings.Server.Substring(slash + 1);
            }

            return builder.ConnectionString;
        }
    }
}
