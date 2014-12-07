using Raven.Client;
using StructureMap;

namespace ReadModelDB
{
    public class UnitOfWork : IUnitOfWork
    {
        private RavenDocStore DocStore { get; set; }
        public IDocumentSession Session { get; set; }

        public UnitOfWork():this("mr_readmodel")
        { 
        }

        public UnitOfWork(string accountCode)
        {
            var docStore = ObjectFactory.GetInstance<RavenDocStore>();
            DocStore = docStore;

            Session = DocStore.OpenSession(accountCode);
            Session.Advanced.UseOptimisticConcurrency = true; 
        }

        public void Dispose()
        {
            Session.Dispose();
        }

    }
}
