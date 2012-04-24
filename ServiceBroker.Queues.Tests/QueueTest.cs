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
         CreateDatabaseIfNotExists( connectionString );
         try
         {
            var storage = new QueueStorage( connectionString );
            storage.Initialize();
         }
         catch (SqlException)
         {
            new SchemaManager().Install( connectionString, 2204 );
         }
         StorageUtil.PurgeAll( connectionString );
      }

      protected string ConnectionString
      {
         get { return ConfigurationManager.ConnectionStrings["testqueue"].ConnectionString; }
      }

      private void CreateDatabaseIfNotExists( string connectionString )
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
            END",
                  databaseName );

            command.CommandType = CommandType.Text;
            command.ExecuteNonQuery();
         }
      }
   }
}