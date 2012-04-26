using System;
using System.Collections.Specialized;
using System.Reflection;
using Common.Logging;

namespace ServiceBroker.Install
{
   public class Program
   {
      static int Main( string[] args )
      {
         var version = Assembly.GetAssembly( typeof (Queues.Storage.SchemaManager) ).GetName().Version.ToString();

         Console.WriteLine( "ServiceBroker.Queues Commandline installer " + version );
         Console.WriteLine();


         if( args == null || args.Length == 0 )
         {
            Console.WriteLine("Please specify connections string on the command line.");
            return 1;
         }

         // hardwire logging configuration
         var properties = new NameValueCollection
                             {
                                {"level", "DEBUG"},
                                {"showLogName", "false"},
                                {"showDataTime", "false"},
                                {"dateTimeFormat", "yyyy/MM/dd HH:mm:ss:fff"}
                             };
         LogManager.Adapter = new Common.Logging.Simple.ConsoleOutLoggerFactoryAdapter( properties );

         // Run install
         try
         {
            Queues.Storage.SchemaManager.Install( args[0] );
            return 0;
         }
         catch (Exception ex)
         {
            var logger = LogManager.GetLogger<Program>();
            logger.Error( ex.GetBaseException().Message );
            return 1;
         }
      }
   }
}
