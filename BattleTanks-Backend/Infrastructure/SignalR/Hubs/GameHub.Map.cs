using System.Threading.Tasks;
using Application.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace Infrastructure.SignalR.Hubs;

public partial class GameHub : Hub
{
    // Send map state to caller
    public async Task GetMap()
    {
        if (!_tracker.TryGet(Context.ConnectionId, out var info))
            throw new HubException("not_in_room");

        var map = await _rooms.GetMapAsync(info.RoomId);
        await Clients.Caller.SendAsync("mapState", map);
    }

    // Destroy a cell and broadcast update
    public async Task DestroyCell(int x, int y)
    {
        if (!_tracker.TryGet(Context.ConnectionId, out var info))
            throw new HubException("not_in_room");

        var updated = await _rooms.DestroyCellAsync(info.RoomId, x, y);
        if (updated != null)
        {
            await Clients.Group(info.RoomCode).SendAsync("cellDestroyed", updated);
            // Broadcast destruction via MQTT and record history
            try
            {
                await _mqtt.PublishAsync($"game/{info.RoomCode}/events/cellDestroyed", updated);
                await _history.AddEventAsync(info.RoomCode, "cellDestroyed", updated);
            }
            catch { }
        }
    }

    // Assign spawn point to caller
    public async Task RequestSpawn()
    {
        if (!_tracker.TryGet(Context.ConnectionId, out var info))
            throw new HubException("not_in_room");

        var spawn = await _rooms.AllocateSpawnAsync(info.RoomId, info.UserId);
        await Clients.Caller.SendAsync("spawnAssigned", new { x = spawn.X, y = spawn.Y });
    }
}
