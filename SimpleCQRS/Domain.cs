using System;
using System.Collections;
using System.Collections.Generic;

namespace SimpleCQRS
{
    public class InventoryItem : AggregateRoot
    {
        private bool _activated;
        private Guid _id;
        public override Guid Id
        {
            get { return _id; }
        }
        public int Count { get; set; }
        public string Name { get; set; }

        private void Apply(InventoryItemCreated e)
        {
            _id = e.Id;
            Name = e.Name;
            _activated = true;
        }

        private void Apply(InventoryItemDeactivated e)
        {
            _activated = false;
        }

        private void Apply(ItemsCheckedInToInventory e)
        {
            Count += e.Count;
        }

        private void Apply(ItemsRemovedFromInventory e)
        {
            Count -= e.Count;
        }

        private void Apply(InventoryItemRenamed e)
        {
            Name = e.NewName;
        }

        public void ChangeName(string newName)
        {
            if (string.IsNullOrEmpty(newName)) throw new ArgumentException("newName");
            ApplyEvent(new InventoryItemRenamed(_id, newName));
        }

        public void Remove(int count)
        {
            if (count <= 0) throw new InvalidOperationException("cant remove negative count from inventory");
            ApplyEvent(new ItemsRemovedFromInventory(_id, count));
        }

        public void CheckIn(int count)
        {
            if (count <= 0) throw new InvalidOperationException("must have a count greater than 0 to add to inventory");
            ApplyEvent(new ItemsCheckedInToInventory(_id, count));
        }

        public void Deactivate()
        {
            if (!_activated) throw new InvalidOperationException("already deactivated");
            ApplyEvent(new InventoryItemDeactivated(_id));
        }

        public InventoryItem()
        {
            // used to create in repository ... many ways to avoid this, eg making private constructor
        }

        public InventoryItem(Guid id, string name)
        {
            ApplyEvent(new InventoryItemCreated(id, name));
        }
    }

    public interface IAggregateRoot
    {
        Guid Id { get; }
        
        void ApplyEvent(object @event);
        void ApplyHistory(object @event);
        ICollection GetUncommittedEvents();
        void ClearUncommittedEvents();
    }
    public abstract class AggregateRoot : IAggregateRoot
    {
        private readonly List<Event> _changes = new List<Event>();

        public void ApplyEvent(object @event)
        {
            var e = @event as Event;
            ApplyChange(e, true);
        }

        public void ApplyHistory(object @event)
        {
            var e = @event as Event;
            ApplyChange(e, false);
        }
        public ICollection GetUncommittedEvents()
        {
            return _changes;
        }

        public void ClearUncommittedEvents()
        {
            _changes.Clear();
        }

        public abstract Guid Id { get; }
        

        private void ApplyChange(Event @event, bool isNew)
        {
            
            var o = this.AsDynamic();
            o.Apply(@event);
            if (isNew) _changes.Add(@event);
        }
    }

    public interface IRepository<T> where T : AggregateRoot, new()
    {
        void Save(AggregateRoot aggregate);
        T GetById(Guid id);
    }

    public class Repository<T> : IRepository<T> where T : AggregateRoot, new() //shortcut you can do as you see fit with new()
    {
        private readonly IGetEventStoreRepository _storage;

        public Repository(IGetEventStoreRepository storage)
        {
            _storage = storage;
        }

        public void Save(AggregateRoot aggregate)
        {
            _storage.Save(aggregate, aggregate.Id, x => { });
        }

        public T GetById(Guid id)
        {
            var e = _storage.GetById<T>(id);
            return e;
        }
    }

}

