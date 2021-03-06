using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using Common.Logging;

namespace ServiceBroker.Queues.Storage
{
    internal class QueueActions : AbstractActions
    {
       private readonly Uri queueUri;
       private readonly ILog logger = LogManager.GetLogger(typeof (QueueActions));
       private readonly ISerializationService serializationService;

       public QueueActions( Uri queueUri, SqlConnection connection, ISerializationService serializationService = null ) : base( connection )
       {
          if ( queueUri == null )
             throw new ArgumentNullException( "queueUri" );
          this.queueUri = queueUri;
          this.serializationService = serializationService ?? new DefaultSerializationService();
       }

       public MessageEnvelope Peek()
       {
          MessageEnvelope message = null;
          ExecuteCommand( "[SBQ].[Peek]", cmd =>
          {
             cmd.CommandType = CommandType.StoredProcedure;
             cmd.Parameters.AddWithValue( "@queueName", queueUri.ToServiceName() );
             using ( var reader = cmd.ExecuteReader( CommandBehavior.Default ) )
             {
                if ( !reader.Read() )
                {
                   message = null;
                   return;
                }

                message = Fill( reader );
             }
          } );
          return message;
       }

        public MessageEnvelope Dequeue( TimeSpan? timeout = null )
        {
            MessageEnvelope message = null;
            ExecuteCommand("[SBQ].[Dequeue]", cmd =>
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@queueName", queueUri.ToServiceName());
                if( timeout.HasValue )
                {
                   if( timeout.Value > TimeSpan.FromSeconds( 25 ) )
                   {
                      cmd.CommandTimeout = 5000 + (int)timeout.Value.TotalMilliseconds;
                   }
                   cmd.Parameters.AddWithValue( "@timeout", timeout.Value.TotalMilliseconds );
                }
                try
                {
                   using (var reader = cmd.ExecuteReader( CommandBehavior.Default ))
                   {
                      if ( !reader.Read() )
                      {
                         message = null;
                         return;
                      }

                      message = Fill( reader );
                      logger.DebugFormat( "Received message {0} from queue {1}", message.ConversationId, queueUri );
                   }
                }
                catch ( SqlException )
                {
                   if ( !isCancelled )
                      throw;
                }
            });
            return message;
        }

       public void RegisterToSend(Uri destination, MessageEnvelope payload)
        {
           byte[] data = serializationService.Serialize( payload );
            ExecuteCommand("[SBQ].[RegisterToSend]", cmd =>
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@localServiceName", queueUri.ToServiceName());
                cmd.Parameters.AddWithValue("@address", destination.ToServiceName());
                cmd.Parameters.AddWithValue("@route", string.Format("{0}://{1}", destination.Scheme, destination.Authority));
                cmd.Parameters.AddWithValue("@sizeOfData", payload.Data.Length);
                cmd.Parameters.AddWithValue("@deferProcessingUntilTime",
                                            (object)payload.DeferProcessingUntilUtcTime ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@sentAt", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@data", data);
                cmd.ExecuteNonQuery();
            });
            logger.DebugFormat( "Created output message from '{0}' to '{1}'", queueUri, destination );
        }

        private MessageEnvelope Fill( IDataRecord reader )
        {
           var conversationId = reader.GetGuid( 0 );
           var messageEnvelope = serializationService.Deserialize( (byte[]) reader.GetValue( 1 ) );
           messageEnvelope.ConversationId = conversationId;
           return messageEnvelope;
        }
    }
}