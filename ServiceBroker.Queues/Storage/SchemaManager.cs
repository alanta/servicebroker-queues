using System;
using System.Data.SqlClient;
using System.Globalization;
using Common.Logging;
using DbUp;
using DbUp.Engine.Output;
using ServiceBroker.Queues.Scripts;

namespace ServiceBroker.Queues.Storage
{
   /// <summary>
   /// Helps install / upgrade the database schema.
   /// </summary>
   public class SchemaManager
   {
      private readonly ILog logger = LogManager.GetLogger<SchemaManager>();

      /// <summary>
      /// Gets the supported database schema version.
      /// </summary>
      /// <value>The supported database schema version.</value>
      public static string SchemaVersion { get { return "1.1"; } }

      /// <summary>
      /// Installs the schema into the database specified by the connection string.
      /// </summary>
      /// <remarks>The user should have all rights on the database.</remarks>
      /// <param name="connectionString">The database connection string.</param>
      /// <param name="port">The TCP port for the end point or <c>null</c> for the default (2204).</param>
      public void Install( string connectionString, int? port = null )
      {
         var updater = DeployChanges.To
            .SqlDatabase( connectionString, "SBQ" )
            .WithScripts( new NormalizedEmbeddedScriptProvider( script => script.EndsWith( ".sql" ) ) )
            .LogTo( new UpgradeLogAdapter( logger ) )
            .Build();

         if ( updater.IsUpgradeRequired() )
         {
            var result = updater.PerformUpgrade();
            if ( !result.Successful )
            {
               throw new InvalidOperationException( "Unable to upgrade schema for ServiceBroker." );
            }
         }

         ConfigureEndPoint( connectionString, port );
      }

      /// <summary>
      /// Configures the service broker TCP end point. If the endpoint already exists it will be recreated.
      /// </summary>
      /// <param name="connectionString">The connection string.</param>
      /// <param name="port">The TCP port or <c>null</c> for the default value (2204).</param>
      public void ConfigureEndPoint( string connectionString, int? port = null )
      {
         var newPort = port ?? 2204;

         using ( var connection = new SqlConnection( connectionString ) )
         {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "select port from sys.tcp_endpoints e WHERE e.[name]='ServiceBusEndPoint'";

            var currentPort = (int?) command.ExecuteScalar();

            if ( currentPort.HasValue && currentPort == newPort )
            {
               return; // nothing to do
            }

            if( currentPort.HasValue )
            {
               command.CommandText = "DROP ENDPOINT ServiceBusEndpoint";
               command.ExecuteNonQuery();
            }

            command.CommandText =
               @"CREATE ENDPOINT ServiceBusEndpoint
   STATE = STARTED
   AS TCP
   (
      LISTENER_PORT = $port$
   )
   FOR SERVICE_BROKER
   (
      AUTHENTICATION = WINDOWS
   )"
               .Replace( "$port$", newPort.ToString( CultureInfo.InvariantCulture ) );
            command.ExecuteNonQuery();

            command.CommandText = @"USE master;
GRANT CONNECT ON ENDPOINT::ServiceBusEndpoint TO [public];";

            command.ExecuteNonQuery();
         }
      }

      private class UpgradeLogAdapter : IUpgradeLog
      {
         private readonly ILog log;

         public UpgradeLogAdapter( ILog log )
         {
            if ( log == null ) throw new ArgumentNullException( "log" );
            this.log = log;
         }

         public void WriteInformation( string format, params object[] args )
         {
            log.InfoFormat( format, args );
         }

         public void WriteError( string format, params object[] args )
         {
            log.ErrorFormat( format, args );
         }

         public void WriteWarning( string format, params object[] args )
         {
            log.WarnFormat( format, args );
         }
      }
   }
}