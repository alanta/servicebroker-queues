using System;
using System.Diagnostics;
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
        private readonly object newMessageArrivedLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueManager"/> class.
        /// </summary>
        /// <param name="connectionString">The database connection string.</param>
        public QueueManager(string connectionString)
        {
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

            if (purgeOldDataTimer != null)
            {
                purgeOldDataTimer.Dispose();
            }

            wasDisposed = true;
        }

        private void AssertNotDisposed()
        {
            if (wasDisposed)
                throw new ObjectDisposedException("QueueManager");
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
        /// Peeksa  the specified queue.
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
              lock ( newMessageArrivedLock )
              {
                 var message = GetMessageFromQueue( queueUri, remaining );
                 if ( message != null )
                    return message;

                 remaining = timeout.Value - sp.Elapsed;

                 if ( remaining <= TimeSpan.Zero || Monitor.Wait( newMessageArrivedLock, remaining ) == false )
                 {
                    return null;
                 }
              }
           }
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