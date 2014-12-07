using System;
using System.IO;
using System.Configuration;
using System.Linq;
using System.ComponentModel.Composition.Hosting;
using Raven.Client.Document;
using Raven.Client.Extensions;
using Raven.Client.Indexes;
using Raven.Imports.Newtonsoft.Json;

namespace ReadModelDB
{
    public class RavenDocStore : DocumentStore
    {
        public static readonly string DefaultDB = "mr_readmodel";

        public RavenDocStore(string serverUrl = null)
        {
            Url = serverUrl ?? ConfigurationManager.AppSettings["RavenDB"];
            Conventions = new DocumentConvention()
                               {
                                   DisableProfiling = true,
                                   ShouldCacheRequest = x => true,
                                   MaxNumberOfRequestsPerSession = 2048,
                                   IdentityPartsSeparator = "_",
                                   SaveEnumsAsIntegers = true,
                                   DefaultQueryingConsistency = ConsistencyOptions.QueryYourWrites
                               };
            
            Initialize();

            var checkDbs = ConfigurationManager.AppSettings.Get("checkdbsandindexes");
            if (checkDbs != null)
            {
                DatabaseCommands.EnsureDatabaseExists(DefaultDB);
                var lfCmds = DatabaseCommands.ForDatabase(DefaultDB);
                IndexCreation.CreateIndexes(
                    new CompositionContainer(
                        new AssemblyCatalog(typeof(ReadModelDB.Indexes).Assembly)), lfCmds, Conventions);

            }
        }

    }
}
