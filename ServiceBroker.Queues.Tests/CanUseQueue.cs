using System;
using System.Threading;
using ServiceBroker.Queues.Storage;
using Xunit;

namespace ServiceBroker.Queues.Tests
{
   public class CanUseQueue : QueueTest
    {
        private readonly Uri queueUri = new Uri("tcp://localhost:2204/h");
        private readonly QueueStorage qf;

        public CanUseQueue()
        {
           EnsureStorage( ConnectionString );
            qf = new QueueStorage( ConnectionString );
            qf.Initialize();
            qf.Global(actions =>
            {
                actions.BeginTransaction();
                actions.CreateQueue(queueUri);
                actions.Commit();
            });
        }

        [Fact]
        public void CanPutSingleMessageInQueue()
        {
           qf.Queue( queueUri, actions =>
            {
                actions.BeginTransaction();
                actions.RegisterToSend(queueUri, new MessageEnvelope
                {
                    Data = new byte[] { 13, 12, 43, 5 },
                });
                actions.Commit();
            });
            Thread.Sleep(30);
            qf.Queue( queueUri, actions =>
            {
                actions.BeginTransaction();
                var message = actions.Dequeue();

                Assert.Equal(new byte[] { 13, 12, 43, 5 }, message.Data);
                actions.Commit();
            });
        }

        [Fact]
        public void WillGetMessagesBackInOrder()
        {
           qf.Queue( queueUri, actions =>
            {
                actions.BeginTransaction();
                actions.RegisterToSend(queueUri, new MessageEnvelope
                {
                    Data = new byte[] { 1 },
                });
                actions.Commit();
            });
            Thread.Sleep(10);
            qf.Queue( queueUri, actions =>
            {
                actions.BeginTransaction();
                actions.RegisterToSend(queueUri, new MessageEnvelope
                {
                    Data = new byte[] { 2 },
                });
                actions.Commit();
            });
            Thread.Sleep(10);
            qf.Queue( queueUri, actions =>
            {
                actions.BeginTransaction();
                actions.RegisterToSend(queueUri, new MessageEnvelope
                {
                    Data = new byte[] { 3 },
                });
                actions.Commit();
            });

            Thread.Sleep(300);
            MessageEnvelope m1 = null;
            MessageEnvelope m2 = null;
            MessageEnvelope m3 = null;

            qf.Queue( queueUri, actions =>
            {
                actions.BeginTransaction();
                m1 = actions.Dequeue();
                actions.Commit();
            });
            qf.Queue( queueUri, actions =>
            {
                actions.BeginTransaction();
                m2 = actions.Dequeue();
                actions.Commit();
            });
            qf.Queue( queueUri, actions =>
            {
                actions.BeginTransaction();
                m3 = actions.Dequeue();
                actions.Commit();
            });
            Assert.Equal(new byte[] { 1 }, m1.Data);
            Assert.Equal(new byte[] { 2 }, m2.Data);
            Assert.Equal(new byte[] { 3 }, m3.Data);
        }

        [Fact]
        public void WillNotGiveMessageToTwoClient()
        {
           qf.Queue( queueUri, actions =>
            {
                actions.BeginTransaction();
                actions.RegisterToSend(queueUri, new MessageEnvelope
                {
                    Data = new byte[] { 1 },
                });
                actions.Commit();
            });

            Thread.Sleep(30);
            qf.Queue( queueUri, actions =>
            {
                actions.BeginTransaction();
                actions.RegisterToSend(queueUri, new MessageEnvelope
                {
                    Data = new byte[] { 2 },
                });
                actions.Commit();
            });

            Thread.Sleep(30);

            qf.Queue( queueUri, actions =>
            {
                actions.BeginTransaction();
                var m1 = actions.Dequeue();
                MessageEnvelope m2 = null;

                qf.Queue( queueUri, queuesActions =>
                {
                    queuesActions.BeginTransaction();
                    m2 = queuesActions.Dequeue();

                    queuesActions.Commit();
                });
                Assert.True(m2 == null || (m2.Data != m1.Data));
                actions.Commit();
            });
        }

        [Fact]
        public void WillGiveNullWhenNoItemsAreInQueue()
        {
           qf.Queue( queueUri, actions =>
            {
                var message = actions.Dequeue();
                Assert.Null(message);
                actions.Commit();
            });
        }
    }
}