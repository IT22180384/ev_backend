/*
 * FILE: MongoDbContext.cs
 * 
 * Database context class for MongoDB operations.
 * Provides access to MongoDB collections and handles connection configuration.
 * Enhanced with better error handling and logging.
 */
using MongoDB.Driver;
using EV_Charging.Api.Models;
using Microsoft.Extensions.Logging;

namespace EV_Charging.Api.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;
        private readonly ILogger<MongoDbContext> _logger;

        public MongoDbContext(IConfiguration configuration, ILogger<MongoDbContext> logger)
        {
            _logger = logger;

            try
            {
                // Initialize MongoDB connection with configuration from environment variables or app settings
                var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") 
                    ?? configuration["MongoDB:ConnectionString"] 
                    ?? "mongodb://localhost:27017";
                    
                var databaseName = Environment.GetEnvironmentVariable("MONGODB_DATABASE_NAME") 
                    ?? configuration["MongoDB:DatabaseName"] 
                    ?? "EVChargingDB";

                Console.WriteLine($"🔄 Attempting to connect to MongoDB...");
                Console.WriteLine($"   Connection: {connectionString}");
                Console.WriteLine($"   Database: {databaseName}");

                _logger.LogInformation("Connecting to MongoDB: {DatabaseName} at {ConnectionString}", 
                    databaseName, 
                    GetSanitizedConnectionString(connectionString));

                var client = new MongoClient(connectionString);
                _database = client.GetDatabase(databaseName);

                // Test connection
                _database.RunCommandAsync((Command<MongoDB.Bson.BsonDocument>)"{ping:1}").Wait();
                _logger.LogInformation("MongoDB connection established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to MongoDB");
                throw;
            }
        }

        public IMongoCollection<Reservation> Reservations => _database.GetCollection<Reservation>("reservations");
        public IMongoCollection<User> Users => _database.GetCollection<User>("users");
        public IMongoCollection<EVOwner> EVOwners => _database.GetCollection<EVOwner>("evowners");
        public IMongoCollection<Operator> Operators => _database.GetCollection<Operator>("Operators");
        
        // Additional collections for QR controller
        public IMongoCollection<Booking> GetBookingsCollection() => _database.GetCollection<Booking>("Bookings");
        public IMongoCollection<Station> GetStationsCollection() => _database.GetCollection<Station>("Stations");

        // Expose the database instance for direct access
        public IMongoDatabase GetDatabase() => _database;

        // Helper method to sanitize connection string for logging
        private string GetSanitizedConnectionString(string connectionString)
        {
            try
            {
                var uri = new Uri(connectionString);
                return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            }
            catch
            {
                return "Invalid connection string";
            }
        }
    }
}