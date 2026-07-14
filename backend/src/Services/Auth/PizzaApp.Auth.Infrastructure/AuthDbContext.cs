using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PizzaApp.Auth.Core.Entities;
using PizzaApp.BuildingBlocks.Mongo;

namespace PizzaApp.Auth.Infrastructure;

public class AuthDbContext
{
    public IMongoCollection<User> Users { get; }

    public AuthDbContext(MongoContext ctx)
    {
        RegisterMappings();
        Users = ctx.GetCollection<User>("Users");
    }

    public static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(User)))
        {
            BsonClassMap.RegisterClassMap<User>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(u => u.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }
    }
}
