using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Xunit;

namespace ServiceBroker.Queues.Tests
{
    public class ReceivingFromServiceBrokerQueue : QueueTest, IDisposable
    {
        private readonly QueueManager queueManager;
        private readonly Uri queueUri = new Uri("tcp://localhost:2204/h");

        public ReceivingFromServiceBrokerQueue()
        {
           EnsureStorage( ConnectionString );
           queueManager = new QueueManager( ConnectionString );
           queueManager.CreateQueues( queueUri );
        }

        public void Dispose()
        {
           queueManager.Dispose();
        }

       [Fact]
        public void CanReceiveFromQueue()
        {
            using (var tx = new TransactionScope())
            {
                queueManager.Send(queueUri, queueUri,
                                   new MessageEnvelope
                                   {
                                       Data = Encoding.Unicode.GetBytes("hello"),
                                   });
                tx.Complete();
            }
            Thread.Sleep(50);
            using(var tx = new TransactionScope())
            {
                var message = queueManager.GetQueue(queueUri).Receive();
                Assert.Equal("hello", Encoding.Unicode.GetString(message.Data));
                tx.Complete();
            }

            using (var tx = new TransactionScope())
            {
                var message = queueManager.GetQueue(queueUri).Receive();
                Assert.Null(message);
                tx.Complete();
            }
        }

        [Fact]
        public void WhenRevertingTransactionMessageGoesBackToQueue()
        {
            using (var tx = new TransactionScope())
            {
                queueManager.Send(queueUri, queueUri,
                                   new MessageEnvelope
                                   {
                                       Data = Encoding.Unicode.GetBytes("hello"),
                                   });
                tx.Complete();
            }
            Thread.Sleep(30);

            using (new TransactionScope())
            {
                var message = queueManager.GetQueue(queueUri).Receive();
                Assert.Equal("hello", Encoding.Unicode.GetString(message.Data));
            }
            using (new TransactionScope())
            {
                var message = queueManager.GetQueue(queueUri).Receive();
                Assert.Equal("hello", Encoding.Unicode.GetString(message.Data));
            }
        }

       [Fact]
       public void It_should_wait_for_a_message()
       {
          // Setup
          var block = new AutoResetEvent( false );

          //  Send delayed message and wait for it
          var task = Task.Factory.StartNew( () =>
                                               {
                                                  block.WaitOne();
                                                  Thread.Sleep( 500 );
                                                  using (var tx = new TransactionScope())
                                                  {
                                                     queueManager
                                                        .Send( queueUri, queueUri,
                                                               new MessageEnvelope
                                                                  {Data = Encoding.Unicode.GetBytes( "Relax" )} );
                                                     tx.Complete();
                                                  }
                                               }
             );

          // act
          MessageEnvelope message;
          Stopwatch stopwatch;

          using ( var tx = new TransactionScope() )
          {
             stopwatch = Stopwatch.StartNew();
             block.Set();
             message = queueManager.Receive( queueUri, TimeSpan.FromMilliseconds( 1000 ) );
             stopwatch.Stop();
             tx.Complete();
          }

          task.Wait( 1000 );

          // Verify
          Assert.Equal( "Relax", Encoding.Unicode.GetString( message.Data ) );
          Assert.InRange( stopwatch.ElapsedMilliseconds, 500, 1000 );
       }
    }
}