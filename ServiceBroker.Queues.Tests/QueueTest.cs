using System;
using System.Configuration;
using ServiceBroker.Queues.Storage;
using System.Data.SqlClient;
using System.Data;
using ServiceBroker.Queues.Tests.Utils;

namespace ServiceBroker.Queues.Tests
{
   public abstract class QueueTest
   {
      protected void EnsureStorage( string connectionString )
      {
         bool runInstall = CreateDatabaseIfNotExists( connectionString );

         if( runInstall )
         {
            SchemaManager.Install( connectionString );
         }

         var storage = new QueueStorage( connectionString );

         try
         {
            storage.Initialize();
         }
         catch(Exception ex)
         {
            if ( !( ex is InvalidOperationException ) && !( ex is SqlException ) )
            {
               throw;
            }

            SchemaManager.Install( connectionString );
            storage.Initialize();
         }

         using ( var qManager = new QueueManager( connectionString, port: 2204 ) )
         {
            qManager.ConfigureEndPoint();
         }

         StorageUtil.PurgeAll( connectionString );
      }

      protected string ConnectionString
      {
         get { return ConfigurationManager.ConnectionStrings["testqueue"].ConnectionString; }
      }

      private bool CreateDatabaseIfNotExists( string connectionString )
      {
         var connectionStringBuilder = new SqlConnectionStringBuilder( connectionString );
         var databaseName = connectionStringBuilder.InitialCatalog;
         connectionStringBuilder.InitialCatalog = "master";
         using (var connection = new SqlConnection( connectionStringBuilder.ConnectionString ))
         {
            connection.Open();
            var command = connection.CreateCommand();

            command.CommandText =
               string.Format(
@"IF ((SELECT DB_ID ('{0}')) IS NULL)
BEGIN
   CREATE DATABASE [{0}]
   SELECT CAST( 1 AS BIT )
END
ELSE
   SELECT CAST( 0 AS BIT )",
                  databaseName );

            command.CommandType = CommandType.Text;
            return (bool)command.ExecuteScalar();
         }
      }
   }
}