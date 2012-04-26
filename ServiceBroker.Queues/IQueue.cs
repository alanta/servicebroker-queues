using System;

namespace ServiceBroker.Queues
{
   /// <summary>
   /// A service broker queue.
   /// </summary>
   public interface IQueue
   {
      /// <summary>
      /// Receive a message from this queue and return immediately.
      /// </summary>
      /// <returns>The message at the top of the queue or <c>null</c> if no message is available.</returns>
      MessageEnvelope Receive();
      /// <summary>
      /// Receive a message from this queue, wait for a message if none is available yet.
      /// </summary>
      /// <param name="timeout">The amount of time to wait for a message.</param>
      /// <returns>The message at the top of the queue or <c>null</c> if no message is available.</returns>
      MessageEnvelope Receive( TimeSpan timeout );

      /// <summary>
      /// Sends the specified message to the destination queue.
      /// </summary>
      /// <param name="message">The message.</param>
      /// <param name="destination">The URI of the destination queue or <c>null</c> to post it to this queue.</param>
      void Send( MessageEnvelope message, Uri destination = null );

      /// <summary>
      /// Sends the specified message to the destination queue.
      /// </summary>
      /// <param name="message">The message.</param>
      /// <param name="destination">The destination queue name.</param>
      void Send( MessageEnvelope message, string destination );
   }
}