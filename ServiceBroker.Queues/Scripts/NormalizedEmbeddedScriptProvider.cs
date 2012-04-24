using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DbUp.Engine;
using DbUp.ScriptProviders;

namespace ServiceBroker.Queues.Scripts
{
   /// <summary>
   /// Retrieves upgrade scripts embedded in an assembly.
   /// </summary>
   public sealed class NormalizedEmbeddedScriptProvider : IScriptProvider
   {
      private readonly Assembly assembly;
      private readonly Func<string, string> normalizePath;
      private readonly Func<string, bool> filter;

      /// <summary>
      /// Initializes a new instance of the <see cref="EmbeddedScriptProvider"/> class.
      /// </summary>
      /// <param name="filter">The filter.</param>
      /// <param name="normalizePath">Function used to normalize path names.</param>
      public NormalizedEmbeddedScriptProvider( Func<string, bool> filter, Func<string, string> normalizePath = null )
         : this( Assembly.GetExecutingAssembly(), filter, normalizePath )
      {
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="EmbeddedScriptProvider"/> class.
      /// </summary>
      /// <param name="assembly">The assembly.</param>
      /// <param name="filter">The filter.</param>
      /// <param name="normalizePath">Function used to normalize path names.</param>
      public NormalizedEmbeddedScriptProvider( Assembly assembly, Func<string, bool> filter, Func<string, string> normalizePath = null )
      {
         this.assembly = assembly;
         this.normalizePath = normalizePath ?? NormalizedScriptName;
         this.filter = filter;
      }

      /// <summary>
      /// Gets all scripts that should be executed.
      /// </summary>
      /// <returns></returns>
      public IEnumerable<SqlScript> GetScripts()
      {
         return assembly
             .GetManifestResourceNames()
             .Select( s =>  new { Name = normalizePath( s ), Resource = s } )
             .Where( s => filter( s.Name ) )
             .OrderBy( s => s.Name )
             .Select( s => ReadResourceAsScript( s.Name, s.Resource ) )
             .ToList();
      }

      private SqlScript ReadResourceAsScript( string scriptName, string resource )
      {
         string contents;
         var resourceStream = assembly.GetManifestResourceStream( resource );
         using ( var resourceStreamReader = new StreamReader( resourceStream ) )
         {
            contents = resourceStreamReader.ReadToEnd();
         }

         return new SqlScript( scriptName, contents );
      }

      private string NormalizedScriptName( string resourceName )
      {
         var match = normalizeRegex.Match( resourceName );
         if ( !string.IsNullOrEmpty( match.Groups[1].Value ) )
         {
            return string.Format( "{0}.{1}/{2}", match.Groups[1].Value, match.Groups[2].Value,
                                  match.Groups[3].Value );
         }

         return match.Groups[3].Value;
      }

      private static readonly Regex normalizeRegex = new Regex( @"\.(?>_([0-9]+)\._([0-9]+))?\.(.*)" );
   }
}