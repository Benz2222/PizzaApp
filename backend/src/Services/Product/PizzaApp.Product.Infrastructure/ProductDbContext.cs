using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PizzaApp.BuildingBlocks.Mongo;
using ProductEntity = PizzaApp.Product.Core.Entities.Product;

namespace PizzaApp.Product.Infrastructure;

public class ProductDbContext
{
    public IMongoCollection<ProductEntity> Products { get; }

    public ProductDbContext(MongoContext ctx)
    {
        RegisterMappings();
        Products = ctx.GetCollection<ProductEntity>("Products");
    }

    public static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(ProductEntity)))
        {
            BsonClassMap.RegisterClassMap<ProductEntity>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(p => p.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }
    }
}
