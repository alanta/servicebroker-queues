using System;
using System.Collections.Specialized;

namespace ServiceBroker.Queues
{
   /// <summary>
   /// A service broker message.
   /// </summary>
    public class MessageEnvelope
    {
       /// <summary>
       /// Initializes a new instance of the <see cref="MessageEnvelope"/> class.
       /// </summary>
        public MessageEnvelope()
        {
            Headers = new NameValueCollection();
        }

        /// <summary>
        /// Gets or sets the conversation id.
        /// </summary>
        /// <value>The conversation id.</value>
        public Guid ConversationId { get; set; }

        /// <summary>
        /// Gets or sets the defer processing until UTC time.
        /// </summary>
        /// <value>The defer processing until UTC time.</value>
        public DateTime? DeferProcessingUntilUtcTime { get; set; }

        /// <summary>
        /// Gets or sets the data.
        /// </summary>
        /// <value>The data.</value>
        public byte[] Data { get; set; }

        /// <summary>
        /// Gets or sets the headers.
        /// </summary>
        /// <value>The headers.</value>
        public NameValueCollection Headers { get; set; }
    }
}