    using MongoDB.Driver;
    using SkiShopBot.Models;

    namespace SkiShopBot.Services;
    public class MongoDbService
    {
        private readonly IMongoCollection<Product> _collection;

        public MongoDbService(string connectionString, string databaseName)
        {
            var client = new MongoClient(connectionString);

            var database = client.GetDatabase(databaseName);

            _collection = database.GetCollection<Product>("Items");
        }
        public async Task AddProductAsync(Product product)
        {
            await _collection.InsertOneAsync(product);
        }

        public async Task<List<Product>> GetAllProductsAsync()
        {
            return await _collection.Find(_ => true).ToListAsync();
        }
    }
