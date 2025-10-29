using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;

using Newtonsoft.Json;
using KioskDevice.Models;
using KioskDevice.Services.Advanced;
using KioskDevice.Services.Interfaces;

namespace KioskDevice.Services
{
    public class MqttCommandListener : BackgroundService
    {
        private readonly ILogger<MqttCommandListener> _logger;
        private readonly IConfiguration _config;
        private readonly IDeviceOrchestrator _orchestrator;
        private readonly IDeviceStateManager _stateManager;
        private readonly IEventLogger _eventLogger;
        private readonly IBackendCommunicationService _backendService;
        private readonly IPrinterService _printerService;
        private readonly ICallSystemService _callSystemService;

        private IMqttClient? _client;
        private MqttClientOptions? _options;
        private string _topicRequest = string.Empty;
        private string _mac = string.Empty;

        public MqttCommandListener(
            ILogger<MqttCommandListener> logger,
            IConfiguration config,
            IDeviceOrchestrator orchestrator,
            IDeviceStateManager stateManager,
            IEventLogger eventLogger,
            IBackendCommunicationService backendService,
            IPrinterService printerService,
            ICallSystemService callSystemService)
        {
            _logger = logger;
            _config = config;
            _orchestrator = orchestrator;
            _stateManager = stateManager;
            _eventLogger = eventLogger;
            _backendService = backendService;
            _printerService = printerService;
            _callSystemService = callSystemService;
        }

        private void Initialize()
        {
            var host = _config.GetValue<string>("Mqtt:Host", "localhost");
            var port = _config.GetValue<int>("Mqtt:Port", 1883);
            var username = _config.GetValue<string?>("Mqtt:Username");
            var password = _config.GetValue<string?>("Mqtt:Password");
            var clientId = _config.GetValue<string>("Mqtt:ClientId", $"kiosk-listener");
            var baseTopic = _config.GetValue<string>("Mqtt:BaseTopic", "kiosk");
            _mac = _config.GetValue<string>("Device:MacAddress", _config.GetValue<string>("Device:DeviceId", "KIOSK-001"));
            _topicRequest = $"{baseTopic}/{_mac}/request";

            var builder = new MqttClientOptionsBuilder()
                .WithClientId(clientId + "-listener")
                .WithTcpServer(host, port)
                .WithCleanSession();

            if (!string.IsNullOrWhiteSpace(username))
            {
                builder = builder.WithCredentials(username, password);
            }

            _options = builder.Build();
            _client = new MqttFactory().CreateMqttClient();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Initialize();
            if (_client == null || _options == null) return;

            _client.ApplicationMessageReceivedAsync += async args =>
            {
                try
                {
                    var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
                    var request = JsonConvert.DeserializeObject<ApiResponse<CommandData>>(payload);
                    if (request?.Data == null)
                    {
                        _logger.LogWarning("MQTT: Invalid request payload");
                        return;
                    }
                    await ProcessRequestAsync(request);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"MQTT: Message handling error: {ex.Message}");
                }
            };

            await _client.ConnectAsync(_options, stoppingToken);
            _logger.LogInformation("MQTT listener connected, subscribing to {topic}", _topicRequest);
            await _client.SubscribeAsync(_topicRequest, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
        }

        private async Task ProcessRequestAsync(ApiResponse<CommandData> request)
        {
            // Route by request.Type: PRINT or CALL
            if (string.Equals(request.Type, "PRINT", StringComparison.OrdinalIgnoreCase))
            {
                await HandlePrintAsync(request);
            }
            else if (string.Equals(request.Type, "CALL", StringComparison.OrdinalIgnoreCase))
            {
                await HandleCallAsync(request);
            }
            else
            {
                await _backendService.ReportErrorAsync(request.CommandId, "UNKNOWN_COMMAND", $"Unsupported type: {request.Type}");
            }
        }

        private async Task HandlePrintAsync(ApiResponse<CommandData> request)
        {
            if (request.Data == null) return;

            var canProcess = await _stateManager.CanProcessCommandAsync("PRINT");
            if (!canProcess)
            {
                await _backendService.ReportErrorAsync(request.CommandId, "BUSY", "Device đang xử lý lệnh in khác");
                return;
            }

            var printerReady = await _printerService.IsPrinterReadyAsync();
            if (!printerReady)
            {
                var printerStatus = await _printerService.GetPrinterStatusAsync();
                await _backendService.ReportErrorAsync(request.CommandId, "PRINTER_ERROR", $"Máy in không sẵn sàng: {printerStatus}");
                return;
            }

            await _stateManager.ChangeStateAsync(DeviceState.Printing, "Processing print");
            try
            {
                var printCommand = new PrintCommand
                {
                    TicketNumber = request.Data.TicketNumber,
                    DepartmentName = request.Data.DepartmentName,
                    CounterNumber = request.Data.CounterNumber,
                    FilePath = request.Data.Path
                };

                await _orchestrator.ProcessPrintCommandAsync(printCommand);

                await _eventLogger.LogEventAsync(new DeviceEvent
                {
                    Type = EventType.PrintCompleted,
                    TicketNumber = request.Data.TicketNumber,
                    Description = $"Printed: {request.Data.TicketNumber}"
                });

                await _backendService.SendCommandResultAsync(request.CommandId, true, $"Đã in phiếu {request.Data.TicketNumber}");
            }
            catch (Exception ex)
            {
                await _backendService.ReportErrorAsync(request.CommandId, "PRINTER_ERROR", $"In thất bại: {ex.Message}");
            }
            finally
            {
                await _stateManager.ChangeStateAsync(DeviceState.Ready, "Print completed");
            }
        }

        private async Task HandleCallAsync(ApiResponse<CommandData> request)
        {
            if (request.Data == null) return;

            var canProcess = await _stateManager.CanProcessCommandAsync("CALL");
            if (!canProcess)
            {
                await _backendService.ReportErrorAsync(request.CommandId, "BUSY", "Device đang xử lý lệnh gọi khác");
                return;
            }

            var callSystemStatus = await _callSystemService.GetCallSystemStatusAsync();
            if (callSystemStatus != 1)
            {
                await _backendService.ReportErrorAsync(request.CommandId, "CALL_SYSTEM_ERROR", "Hệ thống gọi không sẵn sàng");
                return;
            }

            await _stateManager.ChangeStateAsync(DeviceState.Calling, "Processing call");
            try
            {
                var callCommand = new CallCommand
                {
                    TicketNumber = request.Data.TicketNumber,
                    DepartmentName = request.Data.DepartmentName,
                    CounterNumber = request.Data.CounterNumber,
                    Status = "CALLING",
                    AudioPath = request.Data.Path
                };

                await _orchestrator.ProcessCallCommandAsync(callCommand);

                await _eventLogger.LogEventAsync(new DeviceEvent
                {
                    Type = EventType.CallCompleted,
                    TicketNumber = request.Data.TicketNumber,
                    Description = $"Called: {request.Data.TicketNumber} to counter {request.Data.CounterNumber}"
                });

                await _backendService.SendCommandResultAsync(request.CommandId, true, $"Đã gọi phiếu {request.Data.TicketNumber}");
            }
            catch (Exception ex)
            {
                await _backendService.ReportErrorAsync(request.CommandId, "CALL_SYSTEM_ERROR", $"Gọi thất bại: {ex.Message}");
            }
            finally
            {
                await _stateManager.ChangeStateAsync(DeviceState.Ready, "Call completed");
            }
        }
    }
}


