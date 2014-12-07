using System;
using System.Collections.Generic;

namespace SimpleCQRS
{
    public interface IGetEventStoreRepository
    {
        T GetById<T>(Guid id) where T : class, IAggregateRoot;
        T GetById<T>(Guid id, int version) where T : class, IAggregateRoot;
        void Save(IAggregateRoot aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders);
    }
}