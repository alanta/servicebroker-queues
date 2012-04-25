using System;
using System.Diagnostics;
using System.Threading;
using System.Transactions;
using Common.Logging;
using ServiceBroker.Queues.Storage;

namespace ServiceBroker.Queues
{
    public class QueueManager : IDisposable
    {
        private volatile bool wasDisposed;
        private readonly Timer purgeOldDataTimer;
        private readonly QueueStorage queueStorage;
        private readonly ILog logger = LogManager.GetLogger(typeof(QueueManager));
        private readonly object newMessageArrivedLock = new object();

        public QueueManager(string connectionString)
           : this( new QueueStorage( connectionString ))
        {
        }

        public QueueManager( QueueStorage storage )
        {
           queueStorage = storage;
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

        public IQueue GetQueue(Uri queueUri)
        {
            return new Queue(this, queueUri);
        }

        public MessageEnvelope Peek(Uri queueUri)
        {
           return PeekAtQueue( queueUri );
        }

        public MessageEnvelope Receive(Uri queueUri)
        {
            return Receive(queueUri, null );
        }

        public MessageEnvelope Receive( Uri queueUri, TimeSpan? timeout )
        {
           EnsureEnlistment();

           if( null == timeout )
              return GetMessageFromQueue( queueUri, null );

           var remaining = timeout.Value;

           while ( true )
           {
              lock ( newMessageArrivedLock )
              {
                 var sp = Stopwatch.StartNew();

                 var message = GetMessageFromQueue( queueUri, remaining );
                 if ( message != null )
                    return message;

                 remaining = remaining - sp.Elapsed;

                 if ( remaining.TotalMilliseconds <= 0 || Monitor.Wait( newMessageArrivedLock, remaining ) == false )
                 {
                    return null;
                 }
              }
           }
        }


        public Uri WaitForQueueWithMessageNotification()
        {
            if(Transaction.Current != null)
                throw new InvalidOperationException("You cannot find queue with messages with an ambient transaction, this method is not MSDTC friendly");

            Uri queueUri = null;
            queueStorage.Global(actions =>
            {
                actions.BeginTransaction();
                queueUri = actions.PollForMessage();
                actions.Commit();
            });
            return queueUri;
        }

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

        public void Send(Uri fromQueue, Uri toQueue, MessageEnvelope payload)
        {
            EnsureEnlistment();

            queueStorage.Global(actions =>
            {
                actions.BeginTransaction();
                actions.GetQueue(fromQueue)
                    .RegisterToSend(toQueue, payload);
                actions.Commit();
            });
        }

        private void EnsureEnlistment()
        {
            AssertNotDisposed();

            if (Transaction.Current == null)
                throw new InvalidOperationException("You must use TransactionScope when using ServiceBroker.Queues");
        }

        private MessageEnvelope GetMessageFromQueue(Uri queueUri, TimeSpan? timeout)
        {
            AssertNotDisposed();
            MessageEnvelope message = null;
            queueStorage.Global(actions =>
            {
                actions.BeginTransaction();
                message = actions.GetQueue(queueUri).Dequeue( timeout );
                actions.Commit();
            });
            return message;
        }

        private MessageEnvelope PeekAtQueue( Uri queueUri )
        {
           AssertNotDisposed();
           MessageEnvelope message = null;
           queueStorage.Global( actions =>
           {
              message = actions.GetQueue( queueUri ).Peek();
           } );
           return message;
        }
    }
}