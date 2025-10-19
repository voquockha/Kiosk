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
    public class HealthCheckBackgroundService : BackgroundService
    {
        private readonly IHealthCheckService _healthCheckService;
        private readonly IEventLogger _eventLogger;
        private readonly IDeviceStateManager _stateManager;
        private readonly ILogger<HealthCheckBackgroundService> _logger;
        private readonly IConfiguration _config;

        public HealthCheckBackgroundService(
            IHealthCheckService healthCheckService,
            IEventLogger eventLogger,
            IDeviceStateManager stateManager,
            ILogger<HealthCheckBackgroundService> logger,
            IConfiguration config)
        {
            _healthCheckService = healthCheckService;
            _eventLogger = eventLogger;
            _stateManager = stateManager;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = _config.GetValue<int>("HealthCheck:IntervalMs", 30000);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _healthCheckService.PerformHealthCheckAsync();

                    if (!result.IsHealthy)
                    {
                        await _stateManager.ChangeStateAsync(DeviceState.Error, "Health check failed");

                        var failedComponents = result.Components
                            .Where(c => !c.Value.IsHealthy)
                            .Select(c => c.Key);

                        await _eventLogger.LogEventAsync(new DeviceEvent
                        {
                            Type = EventType.DeviceError,
                            Description = $"Failed components: {string.Join(", ", failedComponents)}",
                            Metadata = new Dictionary<string, object> { { "components", failedComponents } }
                        });
                    }
                    else
                    {
                        await _stateManager.ChangeStateAsync(DeviceState.Ready, "All systems healthy");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Health check error: {ex.Message}");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }
    }
}