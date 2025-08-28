using System.Collections.Generic;
using System.Threading.Tasks;

namespace Infrastructure.Interfaces
{
    /// <summary>
    /// Provides a simple abstraction for recording and retrieving event history for a room.
    /// Implementations may store events in Redis or another backing store. Events are stored as
    /// serialised JSON strings along with metadata and a timestamp.
    /// </summary>
    public interface IEventHistoryService
    {
        /// <summary>
        /// Appends an event to the history for a given room. Implementations may trim older events
        /// beyond a configurable threshold to keep the list size bounded.
        /// </summary>
        /// <param name="roomCode">The human‑readable code identifying the room.</param>
        /// <param name="eventType">A short string identifying the type of event (e.g. "playerJoined").</param>
        /// <param name="payload">The payload associated with the event. It will be serialised to JSON.</param>
        Task AddEventAsync(string roomCode, string eventType, object payload);

        /// <summary>
        /// Retrieves a list of recent events for the given room. The order is typically most
        /// recent first. If fewer events exist than the requested limit, all available events are returned.
        /// </summary>
        /// <param name="roomCode">The human‑readable code identifying the room.</param>
        /// <param name="limit">The maximum number of events to return.</param>
        Task<IReadOnlyList<string>> GetEventsAsync(string roomCode, int limit = 50);
    }
}