using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PizzaApp.BuildingBlocks.Mongo;
using PizzaApp.Order.Core.Entities;
using OrderEntity = PizzaApp.Order.Core.Entities.Order;

namespace PizzaApp.Order.Infrastructure;

public class OrderDbContext
{
    public IMongoCollection<OrderEntity> Orders { get; }

    public OrderDbContext(MongoContext ctx)
    {
        RegisterMappings();
        Orders = ctx.GetCollection<OrderEntity>("Orders");
    }

    public static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(OrderItem)))
        {
            BsonClassMap.RegisterClassMap<OrderItem>(cm => { cm.AutoMap(); cm.SetIgnoreExtraElements(true); });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(OrderEntity)))
        {
            BsonClassMap.RegisterClassMap<OrderEntity>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(o => o.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }
    }
}
