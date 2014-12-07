using System;
using System.Collections.Generic;
using System.Linq;
using ReadModelDB;

namespace SimpleCQRS
{
    public class InventoryItemDto
    {
        public string Id;
        public string Name;
        public int CurrentCount;
        public int Version;

        public InventoryItemDto(Guid id, string name, int currentCount, int version)
        {
			Id = id.ToString();
			Name = name;
            CurrentCount = currentCount;
            Version = version;
        }
    }

    public class InventoryItemView : Handles<InventoryItemCreated>, 
                                     Handles<InventoryItemDeactivated>, 
                                     Handles<InventoryItemRenamed>, 
                                     Handles<ItemsRemovedFromInventory>, 
                                     Handles<ItemsCheckedInToInventory>

    {
        
        public void Handle(InventoryItemCreated message)
        {
            using (var uow = new UnitOfWork())
            {
                var item = new InventoryItemDto(message.Id, message.Name, 0, 0);
                uow.Session.Store(item, message.Id.ToString());
                uow.Session.SaveChanges();
            }
        }

        public void Handle(InventoryItemRenamed message)
        {
            using (var uow = new UnitOfWork())
            {
                var item = uow.Session.Load<InventoryItemDto>(message.Id.ToString());
                item.Name = message.NewName;
                uow.Session.SaveChanges();
            }
        }
        
        public void Handle(ItemsRemovedFromInventory message)
        {
            using (var uow = new UnitOfWork())
            {
                var item = uow.Session.Load<InventoryItemDto>(message.Id.ToString());
                item.CurrentCount -= message.Count;
                uow.Session.SaveChanges();
            }
        }

        public void Handle(ItemsCheckedInToInventory message)
        {
            using (var uow = new UnitOfWork())
            {
                var item = uow.Session.Load<InventoryItemDto>(message.Id.ToString());
                item.CurrentCount += message.Count;
                uow.Session.SaveChanges();
            }
        }

        public void Handle(InventoryItemDeactivated message)
        {
            using (var uow = new UnitOfWork())
            {
                var item = uow.Session.Load<InventoryItemDto>(message.Id.ToString());
                uow.Session.Delete(item);
                uow.Session.SaveChanges();
                
            }
        }
    }

    public class ReadModelFacade 
    {
        public IEnumerable<InventoryItemDto> GetInventoryItems()
        {
            using (var uow = new UnitOfWork())
            {
                var list = uow.Session.Query<InventoryItemDto>().ToList();
                return list;
            }
            
        }

        public InventoryItemDto GetInventoryItem(Guid id)
        {
            using (var uow = new UnitOfWork())
            {
                return uow.Session.Load<InventoryItemDto>(id.ToString());
            }
        }

        
    }

}
