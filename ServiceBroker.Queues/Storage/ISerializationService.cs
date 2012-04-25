namespace ServiceBroker.Queues.Storage
{
   internal interface ISerializationService
   {
      byte[] Serialize( MessageEnvelope messageEnvelope );
      MessageEnvelope Deserialize( byte[] buffer );
   }
}
