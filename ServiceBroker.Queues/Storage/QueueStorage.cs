using System;
using System.Data.SqlClient;
using System.Data;

namespace ServiceBroker.Queues.Storage
{
   internal class QueueStorage
   {
      private readonly string connectionString;

      public QueueStorage( string connectionString )
      {
         this.connectionString = connectionString;
      }

      public Guid Id { get; private set; }

      public void Initialize()
      {
         SetIdFromDb();
      }

      private void SetIdFromDb()
      {
         using (var connection = new SqlConnection( connectionString ))
         {
            connection.Open();
            using (var sqlCommand = new SqlCommand( "select top 1 * from [SBQ].[Detail]", connection ))
            using (var reader = sqlCommand.ExecuteReader( CommandBehavior.SingleRow ))
            {
               if ( !reader.Read() )
                  throw new InvalidOperationException( "No version detail found in the queue storage" );

               Id = reader.GetGuid( reader.GetOrdinal( "id" ) );
               var schemaVersion = reader.GetString( reader.GetOrdinal( "schemaVersion" ) );
               if ( schemaVersion != SchemaManager.SchemaVersion )
               {
                  throw new InvalidOperationException(
                     string.Format(
                        "The queue schema version in the database is {0}, this library supports version {1}.\nPlease update or re-install by calling SchemaManager.Install.",
                        schemaVersion, SchemaManager.SchemaVersion ) );
               }
            }
         }
      }

      internal void Global( Action<GlobalActions> action )
      {
         using (var connection = new SqlConnection( connectionString ))
         {
            connection.Open();
            var qa = new GlobalActions( connection );
            action( qa );
         }
      }
   }
}