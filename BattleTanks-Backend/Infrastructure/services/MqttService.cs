using System;
using System.Text.Json;
using System.Threading.Tasks;
using Infrastructure.Configuration;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;

namespace Infrastructure.Services
{
    /// <summary>
    /// Implements an MQTT publisher using the MQTTnet library. The client lazily connects on the
    /// first publish and will reuse the connection thereafter. Payloads are serialised as JSON
    /// using the System.Text.Json serializer.
    /// </summary>
    public class MqttService : IMqttService
    {
        private readonly IMqttClient _client;
        private readonly MqttOptions _options;
        private readonly ILogger<MqttService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _isConnecting;

        public MqttService(IOptions<MqttOptions> options, ILogger<MqttService> logger)
        {
            _options = options.Value;
            _logger = logger;
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();
            // Configure JSON to use camelCase property names consistent with the frontend
            _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            _client.DisconnectedAsync += async e =>
            {
                // Attempt to reconnect when disconnected unexpectedly
                if (!_isConnecting)
                {
                    _logger.LogWarning("MQTT client disconnected: {Reason}, attempting to reconnect...", e.Exception?.Message ?? e.ReasonString);
                    try
                    {
                        await ConnectAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to reconnect MQTT client");
                    }
                }
            };
        }

        private async Task ConnectAsync()
        {
            if (_client.IsConnected || _isConnecting) return;
            _isConnecting = true;
            try
            {
                var builder = new MqttClientOptionsBuilder()
                    .WithTcpServer(_options.Host, _options.Port);
                if (!string.IsNullOrWhiteSpace(_options.Username))
                {
                    builder = builder.WithCredentials(_options.Username, _options.Password);
                }
                var clientOptions = builder.Build();
                await _client.ConnectAsync(clientOptions);
                _logger.LogInformation("Connected to MQTT broker at {Host}:{Port}", _options.Host, _options.Port);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        /// <inheritdoc/>
        public async Task PublishAsync(string topic, object payload, bool retain = false, int qos = 0)
        {
            if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("Topic cannot be null or whitespace", nameof(topic));
            // Ensure connected
            await ConnectAsync();
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(json)
                .WithRetainFlag(retain)
                .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)Math.Clamp(qos, 0, 2))
                .Build();
            try
            {
                await _client.PublishAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish MQTT message to {Topic}", topic);
            }
        }
    }
}