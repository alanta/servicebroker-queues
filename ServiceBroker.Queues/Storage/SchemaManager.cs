using System;
using System.Data.SqlClient;
using System.Globalization;
using DbUp;
using ServiceBroker.Queues.Scripts;

namespace ServiceBroker.Queues.Storage
{
   public class SchemaManager
   {
      public static string SchemaVersion { get { return "1.0"; } }

      public void Install( string connectionString, int? port = null )
      {
         var updater = DeployChanges.To
            .SqlDatabase( connectionString, "SBQ" )
            .WithScripts( new NormalizedEmbeddedScriptProvider( script => script.EndsWith( ".sql" ) ) )
            .LogToConsole()
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
   }
}