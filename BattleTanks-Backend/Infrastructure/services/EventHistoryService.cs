using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Infrastructure.Configuration;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Services
{
    public class EventHistoryService : IEventHistoryService
    {
        private readonly IConnectionMultiplexer _connection;
        private readonly int _maxEventsPerRoom;
        private readonly JsonSerializerOptions _jsonOptions;

        public EventHistoryService(IConnectionMultiplexer connection, IOptions<RedisOptions> options)
        {
            _connection = connection;
            _maxEventsPerRoom = options.Value.MaxEventsPerRoom;
            _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        }

        public async Task AddEventAsync(string roomCode, string eventType, object payload)
        {
            if (string.IsNullOrWhiteSpace(roomCode)) return;
            var db = _connection.GetDatabase();
            var key = GetKey(roomCode);
            var entry = JsonSerializer.Serialize(new
            {
                eventType,
                data = payload,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }, _jsonOptions);
            await db.ListLeftPushAsync(key, entry);
            // Trim list to configured length
            await db.ListTrimAsync(key, 0, _maxEventsPerRoom - 1);
        }

        public async Task<IReadOnlyList<string>> GetEventsAsync(string roomCode, int limit = 50)
        {
            var db = _connection.GetDatabase();
            var key = GetKey(roomCode);
            var events = await db.ListRangeAsync(key, 0, limit - 1);
            return events.Select(v => (string)v!).ToList().AsReadOnly();
        }

        private static string GetKey(string roomCode) => $"room:{roomCode}:events";
    }
}