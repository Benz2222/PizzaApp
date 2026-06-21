using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using PizzaApp.Core.Entities;

namespace PizzaApp.Infrastructure.Data;

public class MongoDbService
{
    private readonly IMongoDatabase _database;

    public MongoDbService(IConfiguration configuration)
    {
        var connectionString = configuration["MongoDB:ConnectionString"];
        var databaseName = configuration["MongoDB:DatabaseName"];

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);

        RegisterMappings();
    }

    public IMongoCollection<T> GetCollection<T>(string name)
    {
        return _database.GetCollection<T>(name);
    }

    private void RegisterMappings()
    {
        // Sử dụng StringSerializer(BsonType.String) để chấp nhận mọi loại chuỗi (như "1", "2" hoặc ObjectId)
        if (!BsonClassMap.IsClassMapRegistered(typeof(Product)))
        {
            BsonClassMap.RegisterClassMap<Product>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true); // Bỏ qua field cũ còn sót (vd "Category") để không crash khi đọc
                cm.MapIdProperty(p => p.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance) // Tự sinh ID cho sản phẩm mới
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(Category)))
        {
            BsonClassMap.RegisterClassMap<Category>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(Order)))
        {
            BsonClassMap.RegisterClassMap<Order>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(o => o.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance) // Tự sinh ID cho đơn hàng mới
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(User)))
        {
            BsonClassMap.RegisterClassMap<User>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(u => u.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance) // Ensure inserted users get a string id
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(CartItem)))
        {
            BsonClassMap.RegisterClassMap<CartItem>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(Payment)))
        {
            BsonClassMap.RegisterClassMap<Payment>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(p => p.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }
    }
}
