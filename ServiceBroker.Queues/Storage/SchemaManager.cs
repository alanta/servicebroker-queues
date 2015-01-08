using System;
using System.Data.SqlClient;
using System.Globalization;
using Common.Logging;
using DbUp;
using DbUp.Engine.Output;
using ServiceBroker.Queues.Scripts;

namespace ServiceBroker.Queues.Storage
{
   /// <summary>
   /// Helps install / upgrade the database schema.
   /// </summary>
   public class SchemaManager
   {
      /// <summary>
      /// Gets the supported database schema version.
      /// </summary>
      /// <value>The supported database schema version.</value>
      public static string SchemaVersion { get { return "1.1"; } }

      /// <summary>
      /// Installs the schema into the database specified by the connection string.
      /// </summary>
      /// <remarks>The user should have all rights on the database.</remarks>
      /// <param name="connectionString">The database connection string.</param>
      public static void Install( string connectionString )
      {
         var updater = DeployChanges.To
            .SqlDatabase( connectionString, "SBQ" )
            .WithScripts( new NormalizedEmbeddedScriptProvider( script => script.EndsWith( ".sql" ) ) )
            .LogTo( new UpgradeLogAdapter( LogManager.GetLogger<SchemaManager>() ) )
            .Build();

         if ( updater.IsUpgradeRequired() )
         {
            var result = updater.PerformUpgrade();
            if ( !result.Successful )
            {
               throw new InvalidOperationException( "Unable to upgrade schema for ServiceBroker." );
            }
         }
      }

      
      private class UpgradeLogAdapter : IUpgradeLog
      {
         private readonly ILog log;

         public UpgradeLogAdapter( ILog log )
         {
            if ( log == null ) throw new ArgumentNullException( "log" );
            this.log = log;
         }

         public void WriteInformation( string format, params object[] args )
         {
            log.InfoFormat( format, args );
         }

         public void WriteError( string format, params object[] args )
         {
            log.ErrorFormat( format, args );
         }

         public void WriteWarning( string format, params object[] args )
         {
            log.WarnFormat( format, args );
         }
      }
   }
}