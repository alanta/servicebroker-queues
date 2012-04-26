using System;
using System.Data;
using System.Data.SqlClient;

namespace ServiceBroker.Queues.Storage
{
    internal class GlobalActions : AbstractActions
    {
        public GlobalActions(SqlConnection connection) : base(connection)
        {
        }

        public void CreateQueue(Uri queueUri)
        {
            ExecuteCommand("[SBQ].[CreateQueueIfDoesNotExist]", cmd =>
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@address", queueUri.ToServiceName());
                cmd.ExecuteNonQuery();
            });
        }

        public void DeleteHistoric()
        {
            ExecuteCommand("[SBQ].[PurgeHistoric]", cmd =>
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.ExecuteNonQuery();
            });
        }
    }
}