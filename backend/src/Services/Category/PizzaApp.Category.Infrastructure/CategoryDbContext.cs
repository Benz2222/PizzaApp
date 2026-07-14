using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PizzaApp.BuildingBlocks.Mongo;
using CategoryEntity = PizzaApp.Category.Core.Entities.Category;

namespace PizzaApp.Category.Infrastructure;

public class CategoryDbContext
{
    public IMongoCollection<CategoryEntity> Categories { get; }

    public CategoryDbContext(MongoContext ctx)
    {
        RegisterMappings();
        Categories = ctx.GetCollection<CategoryEntity>("Categories");
    }

    public static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(CategoryEntity)))
        {
            BsonClassMap.RegisterClassMap<CategoryEntity>(cm =>
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
