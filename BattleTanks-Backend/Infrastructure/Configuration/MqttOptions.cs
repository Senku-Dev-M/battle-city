using System;

namespace Infrastructure.Configuration
{
    /// <summary>
    /// Options used to configure the MQTT client. These values are bound from configuration
    /// (appsettings.json: "Mqtt").
    /// </summary>
    public class MqttOptions
    {
        /// <summary>
        /// Hostname or IP address of the MQTT broker. Defaults to "localhost".
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// TCP port of the MQTT broker. Defaults to 1883 which is the standard port for MQTT.
        /// </summary>
        public int Port { get; set; } = 1883;

        /// <summary>
        /// Username for connecting to the broker (optional).
        /// </summary>
        public string? Username { get; set; }
        
        /// <summary>
        /// Password for connecting to the broker (optional).
        /// </summary>
        public string? Password { get; set; }
    }
}