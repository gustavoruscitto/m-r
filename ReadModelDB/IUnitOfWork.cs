using System;
using Raven.Client;

namespace ReadModelDB
{
    public interface IUnitOfWork: IDisposable
    {
        IDocumentSession Session { get; set; }
    }
}
