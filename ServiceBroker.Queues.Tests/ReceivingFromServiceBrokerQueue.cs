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

        public ReceivingFromServiceBrokerQueue()
        {
           EnsureStorage( ConnectionString );
           queueManager = new QueueManager( ConnectionString );
           queueManager.CreateQueues( "h" );
        }

        public void Dispose()
        {
           queueManager.Dispose();
        }

       [Fact]
        public void CanReceiveFromQueue()
       {
          var h = queueManager.GetQueue( "h" );
            using (var tx = new TransactionScope())
            {
                h.Send( (Uri) null, new MessageEnvelope { Data = Encoding.Unicode.GetBytes("hello") });
                tx.Complete();
            }
            Thread.Sleep(50);
            using(var tx = new TransactionScope())
            {
                var message = h.Receive();
                Assert.Equal("hello", Encoding.Unicode.GetString(message.Data));
                tx.Complete();
            }

            using (var tx = new TransactionScope())
            {
                var message = h.Receive();
                Assert.Null(message);
                tx.Complete();
            }
        }

        [Fact]
        public void WhenRevertingTransactionMessageGoesBackToQueue()
        {
           var h = queueManager.GetQueue( "h" );
            using (var tx = new TransactionScope())
            {
                h.Send( (Uri) null, new MessageEnvelope { Data = Encoding.Unicode.GetBytes("hello") });
                tx.Complete();
            }
            Thread.Sleep(30);

            using (new TransactionScope())
            {
                var message = h.Receive();
                Assert.Equal("hello", Encoding.Unicode.GetString(message.Data));
            }
            using (new TransactionScope())
            {
                var message = h.Receive();
                Assert.Equal("hello", Encoding.Unicode.GetString(message.Data));
            }
        }

       [Fact]
       public void It_should_wait_for_a_message()
       {
          // Setup
          var block = new AutoResetEvent( false );
          var h = queueManager.GetQueue( "h" );

          //  Send delayed message and wait for it
          var task = Task.Factory.StartNew(
             () =>
                {
                   block.WaitOne();
                   Thread.Sleep( 500 );
                   using (var tx = new TransactionScope())
                   {
                      h.Send( (Uri) null, new MessageEnvelope {Data = Encoding.Unicode.GetBytes( "Relax" )} );
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
             message = h.Receive( TimeSpan.FromMilliseconds( 1000 ) );
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