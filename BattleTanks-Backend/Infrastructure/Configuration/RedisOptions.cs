namespace Infrastructure.Configuration
{
    /// <summary>
    /// Options used to configure the Redis connection. These values are bound from configuration
    /// (appsettings.json: "ConnectionStrings:Redis" or a dedicated section).
    /// </summary>
    public class RedisOptions
    {
        /// <summary>
        /// Connection string used by StackExchange.Redis to connect to the Redis server.
        /// </summary>
        public string ConnectionString { get; set; } = "localhost:6379";

        /// <summary>
        /// Maximum number of events to retain per room. Older events will be trimmed when new events are added.
        /// </summary>
        public int MaxEventsPerRoom { get; set; } = 200;
    }
}