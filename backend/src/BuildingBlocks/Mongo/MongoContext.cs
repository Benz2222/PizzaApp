using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace PizzaApp.BuildingBlocks.Mongo;

public class MongoContext
{
    private readonly IMongoDatabase _database;

    // Chạy 1 lần cho cả tiến trình, TRƯỚC khi map bất kỳ entity nào.
    static MongoContext()
    {
        RegisterDecimalAsNumber();
    }

    /// <summary>
    /// Mặc định driver lưu decimal thành String -> $sum không cộng được,
    /// sort theo giá sai thứ tự (so sánh chuỗi). Ép lưu thành Decimal128 (số).
    /// </summary>
    private static void RegisterDecimalAsNumber()
    {
        BsonSerializer.RegisterSerializer(typeof(decimal),
            new DecimalSerializer(BsonType.Decimal128));
        BsonSerializer.RegisterSerializer(typeof(decimal?),
            new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));
    }

    public MongoContext(MongoSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);
    }

    public IMongoCollection<T> GetCollection<T>(string name) =>
        _database.GetCollection<T>(name);
}
