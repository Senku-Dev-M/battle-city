using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace BattleTanks_Backend.Controllers
{
    /// <summary>
    /// Provides a simple REST API to access the event history for a particular room. This can be
    /// useful for debugging or for clients that are not using SignalR. Access is restricted to
    /// authenticated users.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EventsController : ControllerBase
    {
        private readonly IEventHistoryService _history;
        public EventsController(IEventHistoryService history)
        {
            _history = history;
        }

        /// <summary>
        /// Gets the most recent events for the specified room. The caller must provide the room
        /// code rather than the GUID. Results are ordered from most recent to oldest. By default
        /// the last 50 events are returned.
        /// </summary>
        /// <param name="roomCode">The human‑readable code of the room.</param>
        /// <param name="limit">Maximum number of events to return (optional, defaults to 50).</param>
        [HttpGet("{roomCode}/{limit:int?}")]
        public async Task<IActionResult> GetHistory(string roomCode, int? limit)
        {
            var events = await _history.GetEventsAsync(roomCode, limit ?? 50);
            return Ok(events);
        }
    }
}