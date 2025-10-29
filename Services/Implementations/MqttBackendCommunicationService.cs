using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
// using MQTTnet.Client.Options; // Not needed in v4
using Newtonsoft.Json;
using KioskDevice.Models;
using KioskDevice.Services.Interfaces;

namespace KioskDevice.Services.Implementations
{
    public class MqttBackendCommunicationService : IBackendCommunicationService, IAsyncDisposable
    {
        private readonly ILogger<MqttBackendCommunicationService> _logger;
        private readonly IConfiguration _config;
        private readonly string _deviceId;
        private readonly string _baseTopic;
        private readonly string _mac;
        private IMqttClient? _client;
        private MqttClientOptions? _options;
        private readonly SemaphoreSlim _connectLock = new(1, 1);

        public MqttBackendCommunicationService(ILogger<MqttBackendCommunicationService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _deviceId = _config.GetValue<string>("Device:DeviceId", "KIOSK-001");
            _mac = _config.GetValue<string>("Device:MacAddress", _deviceId);
            _baseTopic = _config.GetValue<string>("Mqtt:BaseTopic", "kiosk");

            InitializeOptions();
        }

        private void InitializeOptions()
        {
            var host = _config.GetValue<string>("Mqtt:Host", "localhost");
            var port = _config.GetValue<int>("Mqtt:Port", 1883);
            var username = _config.GetValue<string?>("Mqtt:Username");
            var password = _config.GetValue<string?>("Mqtt:Password");
            var clientId = _config.GetValue<string>("Mqtt:ClientId", $"kiosk-{_deviceId}");

            var builder = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithTcpServer(host, port)
                .WithCleanSession();

            if (!string.IsNullOrWhiteSpace(username))
            {
                builder = builder.WithCredentials(username, password);
            }

            _options = builder.Build();
            _client = new MqttFactory().CreateMqttClient();
        }

        private async Task EnsureConnectedAsync()
        {
            if (_client == null || _options == null)
            {
                InitializeOptions();
            }

            if (_client!.IsConnected) return;

            await _connectLock.WaitAsync();
            try
            {
                if (!_client.IsConnected)
                {
                    await _client.ConnectAsync(_options!, CancellationToken.None);
                    _logger.LogInformation("Connected to MQTT broker");
                }
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private async Task PublishAsync(string topic, object payload)
        {
            await EnsureConnectedAsync();
            var json = JsonConvert.SerializeObject(payload);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(json))
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await _client!.PublishAsync(message, CancellationToken.None);
        }

        public Task<ApiResponse<CommandData>> GetPendingCommandsAsync()
        {
            // MQTT is push-based; polling is not applicable here.
            return Task.FromResult<ApiResponse<CommandData>>(default!);
        }

        public async Task<ApiResponse<object>> SendHeartbeatAsync(HeartbeatData heartbeatData)
        {
            // Map heartbeat to audit topic as requested
            var topic = $"{_baseTopic}/{_mac}/audit";
            var envelope = new ApiResponse<HeartbeatData>
            {
                CommandId = Guid.NewGuid().ToString(),
                DeviceId = _deviceId,
                Timestamp = DateTime.UtcNow,
                Type = "HEARTBEAT",
                Message = "HEARTBEAT",
                Status = true,
                Data = heartbeatData
            };

            await PublishAsync(topic, envelope);

            return new ApiResponse<object>
            {
                CommandId = envelope.CommandId,
                DeviceId = _deviceId,
                Timestamp = envelope.Timestamp,
                Type = envelope.Type,
                Message = "Heartbeat published",
                Status = true,
                Data = new { }
            };
        }

        public async Task<ApiResponse<object>> SendCommandResultAsync(string commandId, bool success, string message = "")
        {
            var topic = $"{_baseTopic}/{_mac}/response";
            var payload = new
            {
                commandId,
                deviceId = _deviceId,
                timestamp = DateTime.UtcNow,
                type = "COMMAND_RESULT",
                success,
                message
            };

            await PublishAsync(topic, payload);

            return new ApiResponse<object>
            {
                CommandId = commandId,
                DeviceId = _deviceId,
                Timestamp = DateTime.UtcNow,
                Type = "COMMAND_RESULT",
                Message = "Result published",
                Status = true,
                Data = new { }
            };
        }

        public async Task<ApiResponse<object>> ReportErrorAsync(string commandId, string errorType, string message)
        {
            var topic = $"{_baseTopic}/{_mac}/audit";
            var payload = new
            {
                commandId,
                deviceId = _deviceId,
                timestamp = DateTime.UtcNow,
                type = errorType,
                message
            };

            await PublishAsync(topic, payload);

            return new ApiResponse<object>
            {
                CommandId = commandId,
                DeviceId = _deviceId,
                Timestamp = DateTime.UtcNow,
                Type = errorType,
                Message = "Error published",
                Status = true,
                Data = new { }
            };
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_client != null && _client.IsConnected)
                {
                    await _client.DisconnectAsync();
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                _client?.Dispose();
                _connectLock.Dispose();
            }
        }
    }
}


