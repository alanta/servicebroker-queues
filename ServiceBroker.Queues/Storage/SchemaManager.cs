using System;
using System.Data.SqlClient;
using System.Globalization;
using Common.Logging;
using DbUp;
using DbUp.Engine.Output;
using ServiceBroker.Queues.Scripts;

namespace ServiceBroker.Queues.Storage
{
   public class SchemaManager
   {
      private readonly ILog logger = LogManager.GetLogger<SchemaManager>();

      public static string SchemaVersion { get { return "1.1"; } }

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
         private readonly ILog _log;

         public UpgradeLogAdapter( ILog log )
         {
            if ( log == null ) throw new ArgumentNullException( "log" );
            _log = log;
         }

         public void WriteInformation( string format, params object[] args )
         {
            _log.InfoFormat( format, args );
         }

         public void WriteError( string format, params object[] args )
         {
            _log.ErrorFormat( format, args );
         }

         public void WriteWarning( string format, params object[] args )
         {
            _log.WarnFormat( format, args );
         }
      }
   }
}