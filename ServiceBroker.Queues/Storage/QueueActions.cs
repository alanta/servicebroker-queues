using System;
using System.Data;
using Common.Logging;

namespace ServiceBroker.Queues.Storage
{
    public class QueueActions
    {
        private readonly Uri queueUri;
        private AbstractActions actions;
        private readonly ILog logger = LogManager.GetLogger(typeof (QueueActions));
       private readonly ISerializationService serializationService;

       public QueueActions( Uri queueUri, AbstractActions actions, ISerializationService serializationService = null )
       {
          if ( queueUri == null )
             throw new ArgumentNullException( "queueUri" );
          if ( actions == null )
             throw new ArgumentNullException( "actions" );
          this.queueUri = queueUri;
          this.actions = actions;
          this.serializationService = serializationService ?? new DefaultSerializationService();
       }

       public MessageEnvelope Peek()
       {
          MessageEnvelope message = null;
          actions.ExecuteCommand( "[SBQ].[Peek]", cmd =>
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
            actions.ExecuteCommand("[SBQ].[Dequeue]", cmd =>
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@queueName", queueUri.ToServiceName());
                if( timeout.HasValue )
                {
                   cmd.Parameters.AddWithValue( "@timeout", timeout.Value.TotalMilliseconds );
                }
                using (var reader = cmd.ExecuteReader(CommandBehavior.Default))
                {
                    if (!reader.Read())
                    {
                        message = null;
                        return;
                    }

                    message = Fill(reader);
                    logger.DebugFormat( "Received message {0} from queue {1}", message.ConversationId, queueUri );
                }
            });
            return message;
        }

        public void RegisterToSend(Uri destination, MessageEnvelope payload)
        {
           byte[] data = serializationService.Serialize( payload );
            actions.ExecuteCommand("[SBQ].[RegisterToSend]", cmd =>
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