using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SimpleCQRS
{
    public class GetEventStoreRepository : IGetEventStoreRepository
    {
        private readonly IEventPublisher _publisher;
        private IEventStoreConnection _eventStoreConnection;
        private Func<Type, Guid, string> _aggregateIdToStreamName;
        private static readonly string AggregateClrTypeHeader = "AggregateClrType";
        private static readonly string EventClrTypeHeader = "EventType";
        private static readonly string CommitIdHeader = "CommitId";
        private static JsonSerializerSettings SerializerSettings;
        private static readonly int WritePageSize = 50;
        private static readonly int ReadPageSize = 50;
        
        public GetEventStoreRepository(IEventPublisher publisher, IEventStoreConnection eventStoreConnection)
        {
            _publisher = publisher;
            _eventStoreConnection = eventStoreConnection;
            _aggregateIdToStreamName = (t, id) => id.ToString();
            SerializerSettings = new JsonSerializerSettings(){TypeNameHandling = TypeNameHandling.None};
        }

        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IAggregateRoot
        {
            return GetById<TAggregate>(id, int.MaxValue);
        }

        public TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class, IAggregateRoot
        {
            if (version <= 0)
                throw new InvalidOperationException("Cannot get version <= 0");


            var streamName = _aggregateIdToStreamName(typeof(TAggregate), id);
            var aggregate = ConstructAggregate<TAggregate>();

            var sliceStart = 0; 
            StreamEventsSlice currentSlice;
            do
            {
                var sliceCount = sliceStart + ReadPageSize <= version
                    ? ReadPageSize
                    : version - sliceStart + 1;

                currentSlice = _eventStoreConnection.ReadStreamEventsForwardAsync(streamName, sliceStart, sliceCount, false).Result;

                if (currentSlice.Status == SliceReadStatus.StreamNotFound)
                    throw new Exception("Aggregate not found");

                if (currentSlice.Status == SliceReadStatus.StreamDeleted)
                    throw new Exception("Aggregate deleted");

                sliceStart = currentSlice.NextEventNumber;

                foreach (var evnt in currentSlice.Events)
                {
                    
                    aggregate.ApplyHistory(DeserializeEvent(evnt.OriginalEvent.Metadata, evnt.OriginalEvent.Data));
                }
                    
            } while (version >= currentSlice.NextEventNumber && !currentSlice.IsEndOfStream);

            //if (aggregate.Version != version && version < Int32.MaxValue)
            //    //throw new AggregateVersionException(id, typeof(TAggregate), aggregate.Version, version);
            //    throw new Exception("Aggregate version exception");

            return aggregate;
        }
        private static object DeserializeEvent(byte[] metadata, byte[] data)
        {
           
            var eventClrTypeName = JObject.Parse(Encoding.UTF8.GetString(metadata)).Property(EventClrTypeHeader).Value;
            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), Type.GetType((string)eventClrTypeName));
        }
        private static TAggregate ConstructAggregate<TAggregate>()
        {
            return (TAggregate)Activator.CreateInstance(typeof(TAggregate), true);
        }

        public void Save(IAggregateRoot aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
        {
            var commitHeaders = new Dictionary<string, object>
            {
                {CommitIdHeader, commitId},
                {AggregateClrTypeHeader, aggregate.GetType().AssemblyQualifiedName}
            };
            updateHeaders(commitHeaders);

            var streamName = _aggregateIdToStreamName(aggregate.GetType(), aggregate.Id);
            var newEvents = aggregate.GetUncommittedEvents().Cast<Event>().ToList();
            var eventsToSave = newEvents.Select(e => ToEventData(Guid.NewGuid(), e, commitHeaders)).ToList();

            if (eventsToSave.Count < WritePageSize)
            {
                _eventStoreConnection.AppendToStreamAsync(streamName, ExpectedVersion.Any, eventsToSave);
            }
            else
            {
                var transaction = _eventStoreConnection.StartTransactionAsync(streamName, ExpectedVersion.Any).Result;

                var position = 0;
                while (position < eventsToSave.Count)
                {
                    var pageEvents = eventsToSave.Skip(position).Take(WritePageSize);
                    transaction.WriteAsync(pageEvents).Wait();
                    position += WritePageSize;
                    
                }

                transaction.CommitAsync();
            }
            foreach (var @event in newEvents)
                _publisher.Publish(@event);
                
            aggregate.ClearUncommittedEvents();
        }

        private static EventData ToEventData(Guid eventId, object evnt, IDictionary<string, object> headers)
        {
            var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(evnt, SerializerSettings));

            var eventHeaders = new Dictionary<string, object>(headers)
            {
                {
                    EventClrTypeHeader, evnt.GetType().AssemblyQualifiedName
                }
            };
            var metadata = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(eventHeaders, SerializerSettings));
            var typeName = evnt.GetType().Name;

            return new EventData(eventId, typeName, true, data, metadata);
        }
    
    }
}