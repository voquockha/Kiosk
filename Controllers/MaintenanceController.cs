using KioskDevice.Models;
using KioskDevice.Services.Advanced;
using KioskDevice.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
    
namespace KioskDevice.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MaintenanceController : ControllerBase
    {
        private readonly IDeviceStateManager _stateManager;
        private readonly IConfigurationReloader _configReloader;
        private readonly IPrinterService _printerService;
        private readonly ICallSystemService _callSystemService;
        private readonly IEventLogger _eventLogger;
        private readonly ILogger<MaintenanceController> _logger;

        public MaintenanceController(
            IDeviceStateManager stateManager,
            IConfigurationReloader configReloader,
            IPrinterService printerService,
            ICallSystemService callSystemService,
            IEventLogger eventLogger,
            ILogger<MaintenanceController> logger)
        {
            _stateManager = stateManager;
            _configReloader = configReloader;
            _printerService = printerService;
            _callSystemService = callSystemService;
            _eventLogger = eventLogger;
            _logger = logger;
        }

        /// <summary>
        /// Bật chế độ bảo trì
        /// POST /api/maintenance/start
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartMaintenance([FromBody] MaintenanceRequest request)
        {
            try
            {
                await _stateManager.ChangeStateAsync(DeviceState.Maintenance, request.Reason);

                await _eventLogger.LogEventAsync(new DeviceEvent
                {
                    Type = EventType.DeviceError,
                    Description = $"Maintenance started: {request.Reason}"
                });

                return Ok(new { success = true, message = "Maintenance mode activated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Tắt chế độ bảo trì
        /// POST /api/maintenance/stop
        /// </summary>
        [HttpPost("stop")]
        public async Task<IActionResult> StopMaintenance()
        {
            try
            {
                await _stateManager.ChangeStateAsync(DeviceState.Ready, "Maintenance completed");

                await _eventLogger.LogEventAsync(new DeviceEvent
                {
                    Type = EventType.DeviceOnline,
                    Description = "Maintenance completed"
                });

                return Ok(new { success = true, message = "Device returned to normal operation" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Reload cấu hình
        /// POST /api/maintenance/reload-config
        /// </summary>
        [HttpPost("reload-config")]
        public async Task<IActionResult> ReloadConfiguration()
        {
            try
            {
                await _configReloader.ReloadConfigAsync();
                return Ok(new { success = true, message = "Configuration reloaded" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Clear queue lệnh
        /// POST /api/maintenance/clear-queue
        /// </summary>
        [HttpPost("clear-queue")]
        public async Task<IActionResult> ClearCommandQueue([FromServices] ICommandQueueService queueService)
        {
            try
            {
                await queueService.ClearQueueAsync();
                return Ok(new { success = true, message = "Command queue cleared" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Kiểm tra và làm sạch
        /// POST /api/maintenance/cleanup
        /// </summary>
        [HttpPost("cleanup")]
        public async Task<IActionResult> PerformCleanup()
        {
            try
            {
                // Xóa các file log cũ (>30 ngày)
                var logsDir = new DirectoryInfo("Logs");
                if (logsDir.Exists)
                {
                    var oldFiles = logsDir.GetFiles("*.log")
                        .Where(f => DateTime.UtcNow - f.LastWriteTimeUtc > TimeSpan.FromDays(30));

                    foreach (var file in oldFiles)
                    {
                        file.Delete();
                        _logger.LogInformation($"Deleted old log file: {file.Name}");
                    }
                }

                // Clear temp files
                var tempPath = Path.GetTempPath();
                Directory.Delete(tempPath, true);

                await _eventLogger.LogEventAsync(new DeviceEvent
                {
                    Type = EventType.DeviceOnline,
                    Description = "Cleanup operation completed"
                });

                return Ok(new { success = true, message = "Cleanup completed" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin hệ thống
        /// GET /api/maintenance/system-info
        /// </summary>
        [HttpGet("system-info")]
        public IActionResult GetSystemInfo()
        {
            try
            {
                var systemInfo = new
                {
                    osVersion = Environment.OSVersion.VersionString,
                    processorCount = Environment.ProcessorCount,
                    availableMemory = GC.GetTotalMemory(false),
                    machineName = Environment.MachineName,
                    currentState = _stateManager.GetCurrentState().ToString(),
                    timestamp = DateTime.UtcNow
                };

                return Ok(systemInfo);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}