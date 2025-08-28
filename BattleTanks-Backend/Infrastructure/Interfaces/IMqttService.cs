using System.Threading.Tasks;

namespace Infrastructure.Interfaces
{
    /// <summary>
    /// Abstraction over an MQTT publish-only client. Implementations manage the connection lifecycle
    /// and serialise payloads to JSON before sending them to the broker. Quality of service and
    /// retention policies can be specified per message.
    /// </summary>
    public interface IMqttService
    {
        /// <summary>
        /// Publishes a payload to the specified topic. If not connected, implementations should
        /// lazily establish a connection before sending. Payloads are serialised to JSON.
        /// </summary>
        /// <param name="topic">The MQTT topic to publish to.</param>
        /// <param name="payload">The object payload which will be serialised to JSON.</param>
        /// <param name="retain">When true, sets the retain flag so the broker holds the last
        /// message for new subscribers.</param>
        /// <param name="qos">Quality of service level (0, 1 or 2). QoS 0 is fire‑and‑forget.</param>
        Task PublishAsync(string topic, object payload, bool retain = false, int qos = 0);
    }
}