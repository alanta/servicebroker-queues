using System;
using System.Data.SqlClient;
using System.Data;

namespace ServiceBroker.Queues.Storage
{
    public class QueueStorage
    {
        private readonly string connectionString;

        public QueueStorage(string connectionString)
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
				using (var reader = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
				{
					if ( !reader.Read() )
						throw new InvalidOperationException("No version detail found in the queue storage");

					Id = reader.GetGuid(reader.GetOrdinal("id"));
					var schemaVersion = reader.GetString(reader.GetOrdinal("schemaVersion"));
					if (schemaVersion != SchemaManager.SchemaVersion)
					{
						throw new InvalidOperationException("The version on disk (" + schemaVersion +
															") is different that the version supported by this library: " +
															SchemaManager.SchemaVersion + Environment.NewLine +
															"You need to migrate the database version to the library version, alternatively, if the data isn't important, you can drop the items in the [SBQ] schema and run the scripts to create it.");
					}
				}
			}
        }

        public void Global(Action<GlobalActions> action)
        {
            using (var connection = new SqlConnection( connectionString ))
            {
                connection.Open();
                var qa = new GlobalActions(connection);
                action(qa);
            }
        }
    }
}