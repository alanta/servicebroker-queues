using System;
using System.Data;
using System.Data.SqlClient;

namespace ServiceBroker.Queues.Storage
{
    internal abstract class AbstractActions : IDisposable
    {
        private readonly SqlConnection connection;
        private SqlTransaction transaction;
        private SqlCommand pendingCommand;
        protected bool isCancelled;

        protected AbstractActions(SqlConnection connection)
        {
            this.connection = connection;
        }

        public void BeginTransaction()
        {
            transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        }

        public void Commit()
        {
           if ( transaction == null || ( connection.State & ConnectionState.Open ) == 0 )
              return;
           transaction.Commit();
           transaction = null;
        }

       internal void ExecuteCommand(string commandText, Action<SqlCommand> command)
        {
           var sqlCommand = new SqlCommand( commandText, connection );

           try
           {
              sqlCommand.Transaction = transaction;
              isCancelled = false;
              pendingCommand = sqlCommand;
              command( sqlCommand );
           }
           finally
           {
              pendingCommand = null;
              sqlCommand.Dispose();
           }
        }

       public void Cancel()
       {
          if ( null != pendingCommand )
          {
             isCancelled = true;
             pendingCommand.Cancel();
          }
       }

       public void Dispose()
       {
          if( null != connection && ( connection.State & ConnectionState.Open ) != 0 )
          {
             if( null != pendingCommand )
             {
                pendingCommand.Cancel();
             }

             if ( null != transaction )
             {
                transaction.Rollback();
             }
             connection.Close();
          }
       }
    }
}