using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Transactions;
using Common.Logging;
using ServiceBroker.Queues.Storage;

namespace ServiceBroker.Queues
{
   /// <summary>
   /// Provides access to service broker queues.
   /// </summary>
    public class QueueManager : IDisposable
    {
        private volatile bool wasDisposed;
        private readonly Timer purgeOldDataTimer;
        private readonly QueueStorage queueStorage;
        private readonly ILog logger = LogManager.GetLogger(typeof(QueueManager));
        private readonly Uri baseUri = new Uri( "tcp://localhost:2204" );

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueManager"/> class.
        /// </summary>
        /// <param name="connectionString">The connectionstring.</param>
        /// <param name="scheme">The scheme or <c>null</c> to use the default (tcp).</param>
        /// <param name="host">The host or <c>null</c> for localhost.</param>
        /// <param name="port">The port or <c>null</c> for the default (2204).</param>
        public QueueManager( string connectionString, string scheme = null, string host = null, int? port = null )
        {
           baseUri = new Uri( string.Format( "{0}://{1}:{2}", scheme ?? Uri.UriSchemeNetTcp, host ?? "localhost", port ?? 2204 ) );
           queueStorage = new QueueStorage( connectionString );
           queueStorage.Initialize();
           purgeOldDataTimer = new Timer( PurgeOldData, null, TimeSpan.FromMinutes( 3 ), TimeSpan.FromMinutes( 3 ) );
        }

       private void PurgeOldData(object ignored)
        {
            logger.DebugFormat("Starting to purge old data");
            try
            {
                queueStorage.Global(actions =>
                {
                    actions.BeginTransaction();
                    actions.DeleteHistoric();
                    actions.Commit();
                });
            }
            catch (Exception exception)
            {
                logger.Warn("Failed to purge old data from the system", exception);
            }
        }

       /// <summary>
       /// Releases all resources.
       /// </summary>
        public void Dispose()
        {
            if(wasDisposed)
                return;

            wasDisposed = true;

            if (purgeOldDataTimer != null)
            {
                purgeOldDataTimer.Dispose();
            }
        }

        private void AssertNotDisposed()
        {
            if (wasDisposed)
                throw new ObjectDisposedException("QueueManager");
        }

        /// <summary>
        /// Gets the queue URI.
        /// </summary>
        /// <param name="name">The queue name.</param>
        /// <returns></returns>
        public Uri GetQueueUri( string name )
        {
           return new Uri( baseUri, name );
        }

        /// <summary>
        /// Gets the queue.
        /// </summary>
        /// <param name="name">The name of the queue.</param>
        /// <returns></returns>
        public IQueue GetQueue( string name )
        {
           return GetQueue( GetQueueUri( name ) );
        }

        /// <summary>
        /// Gets the queue.
        /// </summary>
        /// <param name="queueUri">The queue URI.</param>
        /// <returns>The queue.</returns>
        public IQueue GetQueue(Uri queueUri)
        {
           if ( queueUri == null )
              throw new ArgumentNullException( "queueUri" );

           return new Queue(this, queueUri);
        }

        /// <summary>
        /// Peeks ate the specified queue.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The message at the top of the queue or <c>null</c> if no message is available.</returns>
        public MessageEnvelope Peek( string name )
        {
           return Peek( GetQueueUri( name ) );
        }

        /// <summary>
        /// Peek at the specified queue.
        /// </summary>
        /// <param name="queueUri">The queue URI.</param>
        /// <returns>The message at the top of the queue or <c>null</c> if no message is available.</returns>
       public MessageEnvelope Peek(Uri queueUri)
        {
           if ( queueUri == null )
              throw new ArgumentNullException( "queueUri" );

           return PeekAtQueue( queueUri );
        }

       /// <summary>
       /// Receives a message from the specified queue.
       /// </summary>
       /// <param name="name">The queue name.</param>
       /// <param name="timeout">The time to wait for a message or <c>null</c> to return immediately.</param>
       /// <returns>The message at the top of the queue or <c>null</c> if no message is available.</returns>
       public MessageEnvelope Receive( string name, TimeSpan? timeout = null )
       {
          return Receive( GetQueueUri( name ), timeout );
       }

       /// <summary>
       /// Receives a message from the specified queue.
       /// </summary>
       /// <param name="queueUri">The queue URI.</param>
       /// <param name="timeout">The time to wait for a message or <c>null</c> to return immediately.</param>
       /// <returns>The message at the top of the queue or <c>null</c> if no message is available.</returns>
       public MessageEnvelope Receive( Uri queueUri, TimeSpan? timeout = null )
        {
           if ( queueUri == null )
              throw new ArgumentNullException( "queueUri" );

           EnsureEnlistment();

           if( null == timeout )
              return GetMessageFromQueue( queueUri, null );

           var remaining = timeout.Value;

           var sp = Stopwatch.StartNew();

           while ( true )
           {
              var message = GetMessageFromQueue( queueUri, remaining );
              if ( message != null )
                 return message;

              remaining = timeout.Value - sp.Elapsed;

              if ( remaining <= TimeSpan.Zero )
              {
                 return null;
              }
           }
        }

       /// <summary>
       /// Creates the queues.
       /// </summary>
       /// <param name="queueNames">The names of the queues to create.</param>
       public void CreateQueues( params string[] queueNames )
       {
          CreateQueues( queueNames.Select( n => new Uri( baseUri, n ) ).ToArray() );
       }


       /// <summary>
       /// Creates the queues.
       /// </summary>
       /// <param name="queues">The URI's for the queues to create.</param>
        public void CreateQueues(params Uri[] queues)
        {
            foreach (var queue in queues)
            {
                Uri uri = queue;
                queueStorage.Global(actions =>
                {
                    actions.BeginTransaction();
                    actions.CreateQueue(uri);
                    actions.Commit();
                });
            }
        }


        /// <summary>
        /// Sends the specified from queue. This method must be called from within a TransactionScope.
        /// </summary>
        /// <param name="fromQueue">The URI of the source queue.</param>
        /// <param name="toQueue">The URI of the destination queue.</param>
        /// <param name="message">The message to send.</param>
        public void Send(Uri fromQueue, Uri toQueue, MessageEnvelope message)
        {
            EnsureEnlistment();

            queueStorage.Queue( fromQueue, actions =>
            {
                actions.BeginTransaction();
                actions.RegisterToSend(toQueue, message);
                actions.Commit();
            });
        }

        private void EnsureEnlistment()
        {
            AssertNotDisposed();

            if (Transaction.Current == null)
                throw new InvalidOperationException("You must use TransactionScope when using ServiceBroker.Queues");
        }

        private MessageEnvelope GetMessageFromQueue(Uri queueUri, TimeSpan? timeout )
        {
            AssertNotDisposed();
            MessageEnvelope message = null;
            queueStorage.Queue( queueUri, actions =>
            {
                actions.BeginTransaction();
                message = actions.Dequeue( timeout );
                actions.Commit();
            });
            return message;
        }

        private MessageEnvelope PeekAtQueue( Uri queueUri )
        {
           AssertNotDisposed();
           MessageEnvelope message = null;
           queueStorage.Queue( queueUri, actions =>
           {
              message = actions.Peek();
           } );
           return message;
        }
    }
}