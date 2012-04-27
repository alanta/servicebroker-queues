using System;

namespace ServiceBroker.Queues
{
   /// <summary>
   /// A service broker queue.
   /// </summary>
   public interface IQueue
   {
      /// <summary>
      /// Gets the queue URI.
      /// </summary>
      /// <value>The URI.</value>
      Uri Uri { get; }

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
      /// <param name="destination">The URI of the destination queue or <c>null</c> to post it to this queue.</param>
      /// <param name="message">The message.</param>
      void Send( Uri destination, MessageEnvelope message );

      /// <summary>
      /// Sends the specified message to the destination queue.
      /// </summary>
      /// <param name="destination">The destination queue name.</param>
      /// <param name="message">The message.</param>
      void Send( string destination, MessageEnvelope message );
   }
}