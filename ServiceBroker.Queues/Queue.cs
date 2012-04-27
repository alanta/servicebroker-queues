using System;

namespace ServiceBroker.Queues
{
    internal class Queue : IQueue
    {
        private readonly QueueManager queueManager;
        private readonly Uri queueUri;

        public Queue(QueueManager queueManager, Uri queueUri)
        {
            this.queueManager = queueManager;
            this.queueUri = queueUri;
        }

        public Uri Uri
        {
            get { return queueUri; }
        }

        public MessageEnvelope Receive()
        {
            return queueManager.Receive(queueUri);
        }

        public MessageEnvelope Receive(TimeSpan timeout)
        {
            return queueManager.Receive(queueUri, timeout);
        }

        public void Send( string destination, MessageEnvelope message )
        {
           Send( queueManager.GetQueueUri( destination ), message );
        }

       public void Send( Uri destination, MessageEnvelope message )
       {
          if ( message == null ) throw new ArgumentNullException( "message" );

          queueManager.Send( queueUri, destination ?? queueUri, message );
       }
    }
}