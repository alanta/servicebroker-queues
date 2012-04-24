namespace ServiceBroker.Queues.Storage
{
   public interface ISerializationService
   {
      byte[] Serialize( MessageEnvelope messageEnvelope );
      MessageEnvelope Deserialize( byte[] buffer );
   }
}
