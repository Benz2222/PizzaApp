using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PizzaApp.BuildingBlocks.Mongo;
using PaymentEntity = PizzaApp.Payment.Core.Entities.Payment;

namespace PizzaApp.Payment.Infrastructure;

public class PaymentDbContext
{
    public IMongoCollection<PaymentEntity> Payments { get; }

    public PaymentDbContext(MongoContext ctx)
    {
        RegisterMappings();
        Payments = ctx.GetCollection<PaymentEntity>("Payments");
    }

    public static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(PaymentEntity)))
        {
            BsonClassMap.RegisterClassMap<PaymentEntity>(cm =>
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
