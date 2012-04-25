using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Transactions;
using Xunit;

namespace ServiceBroker.Queues.Tests
{
   public class When_peeking_at_a_queue : QueueTest, IDisposable
   {
      private readonly QueueManager queueManager;
      private readonly Uri queueUri = new Uri( "tcp://localhost:2204/h" );

      public When_peeking_at_a_queue()
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
      public void It_should_not_dequeue_the_message()
      {
         // setup
         using ( var tx = new TransactionScope() )
         {
            queueManager.Send( queueUri, queueUri,
                               new MessageEnvelope
                               {
                                  Data = Encoding.Unicode.GetBytes( "hello" ),
                               } );
            tx.Complete();
         }

         // act
         var peeked = queueManager.Peek( queueUri );

         // verify
         Assert.NotNull( peeked );
         MessageEnvelope received;
         using ( var tx = new TransactionScope() )
         {
            received = queueManager.Receive( queueUri );
            tx.Complete();
         }
         Assert.NotNull( received );
         Assert.Equal( peeked.ConversationId, received.ConversationId );
      }
   }
}
