using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using KioskDevice.Models;
using KioskDevice.Services.Advanced;
using KioskDevice.Services.Interfaces;

namespace KioskDevice.Services
{
    public class CommandPollingService : BackgroundService
    {
        private readonly ILogger<CommandPollingService> _logger;
        private readonly IBackendCommunicationService _backendService;
        private readonly IDeviceOrchestrator _orchestrator;
        private readonly IDeviceStateManager _stateManager;
        private readonly IEventLogger _eventLogger;
        private readonly IConfiguration _config;

        public CommandPollingService(
            ILogger<CommandPollingService> logger,
            IBackendCommunicationService backendService,
            IDeviceOrchestrator orchestrator,
            IDeviceStateManager stateManager,
            IEventLogger eventLogger,
            IConfiguration config)
        {
            _logger = logger;
            _backendService = backendService;
            _orchestrator = orchestrator;
            _stateManager = stateManager;
            _eventLogger = eventLogger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var pollingInterval = _config.GetValue<int>("Polling:IntervalMs", 2000);
            var heartbeatInterval = _config.GetValue<int>("Polling:HeartbeatIntervalMs", 5000);
            
            int pollCount = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Lấy lệnh từ backend
                    var commandResponse = await _backendService.GetPendingCommandsAsync();

                    if (commandResponse != null && commandResponse.Status && !string.IsNullOrEmpty(commandResponse.Type))
                    {
                        _logger.LogInformation($"Received command: {commandResponse.Type} (ID: {commandResponse.CommandId})");

                        try
                        {
                            // Xử lý lệnh PRINT
                            if (commandResponse.Type == "PRINT")
                            {
                                var printCmd = new PrintCommand
                                {
                                    TicketNumber = commandResponse.Data?.TicketNumber,
                                    DepartmentName = commandResponse.Data?.DepartmentName,
                                    QueuePosition = commandResponse.Data?.QueuePosition ?? 0,
                                    CreatedAt = commandResponse.Data?.CreatedAt ?? DateTime.UtcNow,
                                    FilePath = commandResponse.Data?.Path
                                };

                                // Kiểm tra state
                                var canProcess = await _stateManager.CanProcessCommandAsync("PRINT");
                                if (!canProcess)
                                {
                                    _logger.LogWarning($"Cannot process PRINT command. Current state: {_stateManager.GetCurrentState()}");
                                    await _backendService.ReportErrorAsync(
                                        commandResponse.CommandId,
                                        "DEVICE_NOT_READY",
                                        $"Device not ready. State: {_stateManager.GetCurrentState()}"
                                    );
                                    continue;
                                }

                                // Khóa device
                                await _stateManager.ChangeStateAsync(DeviceState.Printing, "Processing PRINT from polling");

                                try
                                {
                                    await _orchestrator.ProcessPrintCommandAsync(printCmd);

                                    // Báo cáo thành công
                                    await _backendService.SendCommandResultAsync(
                                        commandResponse.CommandId,
                                        true,
                                        $"Đã in phiếu {printCmd.TicketNumber}"
                                    );

                                    await _eventLogger.LogEventAsync(new DeviceEvent
                                    {
                                        Type = EventType.PrintCompleted,
                                        TicketNumber = printCmd.TicketNumber,
                                        Description = $"Print completed via polling",
                                        Metadata = new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "commandId", commandResponse.CommandId },
                                            { "source", "polling" }
                                        }
                                    });

                                    // Trả lại Ready
                                    await _stateManager.ChangeStateAsync(DeviceState.Ready, "Print completed");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"PRINT command failed: {ex.Message}");
                                    await _backendService.ReportErrorAsync(
                                        commandResponse.CommandId,
                                        "PRINTER_ERROR",
                                        $"Print failed: {ex.Message}"
                                    );
                                    await _stateManager.ChangeStateAsync(DeviceState.Error, ex.Message);
                                }
                            }
                            // Xử lý lệnh CALL
                            else if (commandResponse.Type == "CALL")
                            {
                                var callCmd = new CallCommand
                                {
                                    TicketNumber = commandResponse.Data?.TicketNumber,
                                    DepartmentName = commandResponse.Data?.DepartmentName,
                                    CounterNumber = commandResponse.Data?.CounterNumber,
                                    Status = "CALLING",
                                    AudioPath = commandResponse.Data?.Path
                                };

                                // Kiểm tra state
                                var canProcess = await _stateManager.CanProcessCommandAsync("CALL");
                                if (!canProcess)
                                {
                                    _logger.LogWarning($"Cannot process CALL command. Current state: {_stateManager.GetCurrentState()}");
                                    await _backendService.ReportErrorAsync(
                                        commandResponse.CommandId,
                                        "DEVICE_NOT_READY",
                                        $"Device not ready. State: {_stateManager.GetCurrentState()}"
                                    );
                                    continue;
                                }

                                // Khóa device
                                await _stateManager.ChangeStateAsync(DeviceState.Calling, "Processing CALL from polling");

                                try
                                {
                                    await _orchestrator.ProcessCallCommandAsync(callCmd);

                                    // Báo cáo thành công
                                    await _backendService.SendCommandResultAsync(
                                        commandResponse.CommandId,
                                        true,
                                        $"Đã gọi phiếu {callCmd.TicketNumber} quầy {callCmd.CounterNumber}"
                                    );

                                    await _eventLogger.LogEventAsync(new DeviceEvent
                                    {
                                        Type = EventType.CallCompleted,
                                        TicketNumber = callCmd.TicketNumber,
                                        Description = $"Call completed via polling",
                                        Metadata = new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "commandId", commandResponse.CommandId },
                                            { "counter", callCmd.CounterNumber },
                                            { "source", "polling" }
                                        }
                                    });

                                    // Trả lại Ready
                                    await _stateManager.ChangeStateAsync(DeviceState.Ready, "Call completed");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"CALL command failed: {ex.Message}");
                                    await _backendService.ReportErrorAsync(
                                        commandResponse.CommandId,
                                        "CALL_SYSTEM_ERROR",
                                        $"Call failed: {ex.Message}"
                                    );
                                    await _stateManager.ChangeStateAsync(DeviceState.Error, ex.Message);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Command processing error: {ex.Message}");
                            await _backendService.ReportErrorAsync(
                                commandResponse.CommandId,
                                "PROCESSING_ERROR",
                                ex.Message
                            );
                        }
                    }

                    // Gửi heartbeat định kỳ
                    pollCount++;
                    if (pollCount * pollingInterval >= heartbeatInterval)
                    {
                        try
                        {
                            var status = await _orchestrator.GetDeviceStatusAsync();
                            var heartbeatData = new HeartbeatData
                            {
                                Speaker = new DeviceInfo
                                {
                                    Name = "Default Audio Device",
                                    Id = "speaker-1",
                                    Type = "speaker",
                                    Status = status.CallSystemStatus == 1 ? "ok" : "error",
                                    Volume = 80,
                                    IsPlaying = false
                                },
                                Printer = new DeviceInfo
                                {
                                    Name = "EPSON TM-T81III Receipt",
                                    Id = "TM-T81III",
                                    Type = "thermal_printer",
                                    Status = status.PrinterStatus == "Ready" ? "ok" : "error",
                                    Paper = status.PrinterStatus == "Ready" ? "ok" : "error",
                                    Temp = "normal",
                                    IsPrinting = false
                                }
                            };

                            await _backendService.SendHeartbeatAsync(heartbeatData);
                            _logger.LogDebug("Heartbeat sent successfully");
                            pollCount = 0;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Heartbeat error: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Polling error: {ex.Message}");
                }

                await Task.Delay(pollingInterval, stoppingToken);
            }
        }
    }
}
