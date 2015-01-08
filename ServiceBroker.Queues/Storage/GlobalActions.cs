using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace ServiceBroker.Queues.Storage
{
   internal class GlobalActions : AbstractActions
   {
      public GlobalActions( SqlConnection connection )
         : base( connection )
      {
      }

      public void CreateQueue( Uri queueUri )
      {
         ExecuteCommand( "[SBQ].[CreateQueueIfDoesNotExist]", cmd =>
                                                                 {
                                                                    cmd.CommandType = CommandType.StoredProcedure;
                                                                    cmd.Parameters.AddWithValue( "@address",
                                                                                                 queueUri.ToServiceName() );
                                                                    cmd.ExecuteNonQuery();
                                                                 } );
      }

      public void DeleteHistoric()
      {
         ExecuteCommand( "[SBQ].[PurgeHistoric]", cmd =>
                                                     {
                                                        cmd.CommandType = CommandType.StoredProcedure;
                                                        cmd.ExecuteNonQuery();
                                                     } );
      }

      /// <summary>
      /// Configures the service broker TCP end point. If the endpoint already exists it will be recreated.
      /// </summary>
      /// <param name="port">The TCP port or <c>null</c> for the default value (2204).</param>
      public void ConfigureEndPoint( int? port = null )
      {
         var newPort = port ?? 2204;
         int? currentPort = null;

         ExecuteCommand( "select port from sys.tcp_endpoints e WHERE e.[name]='ServiceBusEndPoint'",
                         cmd =>
                            {
                               currentPort = (int?) cmd.ExecuteScalar();
                            } );

         if ( currentPort.HasValue && currentPort == newPort )
         {
            return;
            // nothing to do
         }

         if ( currentPort.HasValue && currentPort != newPort )
         {
            ExecuteCommand( "DROP ENDPOINT ServiceBusEndpoint", command => command.ExecuteNonQuery() );
         }

         ExecuteCommand(
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
               .Replace( "$port$", newPort.ToString( CultureInfo.InvariantCulture ) ),
            command => command.ExecuteNonQuery() );

         ExecuteCommand( @"USE master;
GRANT CONNECT ON ENDPOINT::ServiceBusEndpoint TO [public];",
                         command => command.ExecuteNonQuery() );
      }

      /// <summary>
      /// Adds a route.
      /// </summary>
      /// <param name="name">The name.</param>
      /// <param name="serviceName">Name of the service.</param>
      /// <param name="endpoint">The endpoint.</param>
      /// <param name="brokerInstance">The broker instance.</param>
      public void AddRoute( string name, string serviceName, Uri endpoint, Guid? brokerInstance )
      {
         var sql = string.Format( @"Create Route {0} WITH SERVICE_NAME='{1}' ADDRESS='{2}'", name, serviceName, endpoint );

         if ( brokerInstance.HasValue )
         {
            sql += string.Format( " BROKER_INSTANCE='{0}'", brokerInstance.Value );
         }

         ExecuteCommand( sql, command => command.ExecuteNonQuery() );
      }
   }
}