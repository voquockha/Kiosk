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
        private readonly IConfiguration _config;

        public CommandPollingService(
            ILogger<CommandPollingService> logger,
            IBackendCommunicationService backendService,
            IDeviceOrchestrator orchestrator,
            IConfiguration config)
        {
            _logger = logger;
            _backendService = backendService;
            _orchestrator = orchestrator;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var pollingInterval = _config.GetValue<int>("Polling:IntervalMs", 2000);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Lấy lệnh từ backend
                    var command = await _backendService.GetPendingCommandsAsync();

                    if (command != null)
                    {
                        _logger.LogInformation($"Received command: {command.Type}");

                        if (command.Type == "PRINT")
                        {
                            var printCmd = JsonConvert.DeserializeObject<PrintCommand>(JsonConvert.SerializeObject(command.Data));
                            await _orchestrator.ProcessPrintCommandAsync(printCmd);
                        }
                        else if (command.Type == "CALL")
                        {
                            var callCmd = JsonConvert.DeserializeObject<CallCommand>(JsonConvert.SerializeObject(command.Data));
                            await _orchestrator.ProcessCallCommandAsync(callCmd);
                        }

                        // Báo cáo hoàn thành
                        await _backendService.AcknowledgeCommandAsync(command.CommandId);
                    }

                    // Gửi heartbeat
                    var status = await _orchestrator.GetDeviceStatusAsync();
                    await _backendService.SendHeartbeatAsync(status);
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