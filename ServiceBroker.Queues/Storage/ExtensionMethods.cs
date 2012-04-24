using System;

namespace ServiceBroker.Queues.Storage
{
   public static class ExtensionMethods
   {
      public static string ToServiceName( this Uri uri )
      {
         return uri.Authority + uri.PathAndQuery;
      }
   }
}