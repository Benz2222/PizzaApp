using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PizzaApp.Cart.Core.Entities;
using PizzaApp.BuildingBlocks.Mongo;

namespace PizzaApp.Cart.Infrastructure;

public class CartDbContext
{
    public IMongoCollection<CartItem> CartItems { get; }

    public CartDbContext(MongoContext ctx)
    {
        RegisterMappings();
        CartItems = ctx.GetCollection<CartItem>("CartItems");
    }

    public static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(CartItem)))
        {
            BsonClassMap.RegisterClassMap<CartItem>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }
    }
}
