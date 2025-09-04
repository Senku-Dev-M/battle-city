using System;
using System.Linq;
using Infrastructure.SignalR.Abstractions;
using StackExchange.Redis;

namespace Infrastructure.SignalR.Services;

/// <summary>
/// Implementación de <see cref="IConnectionTracker"/> que almacena las conexiones activas
/// en Redis. Cada conexión se almacena como un campo de un hash, donde la clave es
/// el identificador de la conexión (SignalR ConnectionId) y el valor contiene
/// información serializada del usuario y la sala a la que pertenece.
/// Esta implementación permite que múltiples instancias del servidor compartan
/// información sobre conexiones activas y facilita el escalado horizontal.
/// </summary>
public class RedisConnectionTracker : IConnectionTracker
{
    private readonly IDatabase _db;
    private const string CONNECTIONS_HASH_KEY = "bt:connections";

    public RedisConnectionTracker(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    /// <summary>
    /// Establece o actualiza la información de una conexión.
    /// </summary>
    public void Set(string connectionId, string roomId, string roomCode, string userId, string username)
    {
        if (string.IsNullOrEmpty(connectionId)) return;
        var value = string.Join("|", roomId, roomCode, userId, username);
        _db.HashSet(CONNECTIONS_HASH_KEY, connectionId, value);
    }

    /// <summary>
    /// Obtiene la información de una conexión si existe.
    /// </summary>
    public bool TryGet(string connectionId, out (string RoomId, string RoomCode, string UserId, string Username) info)
    {
        info = default;
        if (string.IsNullOrEmpty(connectionId)) return false;
        var value = _db.HashGet(CONNECTIONS_HASH_KEY, connectionId);
        if (!value.HasValue) return false;
        var parts = ((string)value!).Split('|');
        if (parts.Length != 4) return false;
        info = (parts[0], parts[1], parts[2], parts[3]);
        return true;
    }

    /// <summary>
    /// Elimina una conexión del registro.
    /// </summary>
    public void Remove(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId)) return;
        _db.HashDelete(CONNECTIONS_HASH_KEY, connectionId);
    }

    /// <summary>
    /// Obtiene el número de conexiones asociadas a un código de sala.
    /// </summary>
    public int CountByRoom(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode)) return 0;
        var entries = _db.HashGetAll(CONNECTIONS_HASH_KEY);
        int count = 0;
        foreach (var entry in entries)
        {
            var parts = ((string)entry.Value!).Split('|');
            if (parts.Length >= 2 && parts[1] == roomCode)
            {
                count++;
            }
        }
        return count;
    }

    public string? GetConnectionIdByUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;
        var entries = _db.HashGetAll(CONNECTIONS_HASH_KEY);
        foreach (var entry in entries)
        {
            var parts = ((string)entry.Value!).Split('|');
            if (parts.Length >= 3 && parts[2] == userId)
                return entry.Name!;
        }
        return null;
    }
}