using KioskDevice.Models;
    using KioskDevice.Services.Advanced;
    using KioskDevice.Services.Interfaces;
    using Microsoft.AspNetCore.Mvc;
namespace KioskDevice.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly IHealthCheckService _healthCheckService;
        private readonly IPerformanceMonitor _performanceMonitor;
        private readonly IEventLogger _eventLogger;
        private readonly IPrinterService _printerService;
        private readonly IDisplayService _displayService;
        private readonly ICallSystemService _callSystemService;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(
            IHealthCheckService healthCheckService,
            IPerformanceMonitor performanceMonitor,
            IEventLogger eventLogger,
            IPrinterService printerService,
            IDisplayService displayService,
            ICallSystemService callSystemService,
            ILogger<DiagnosticsController> logger)
        {
            _healthCheckService = healthCheckService;
            _performanceMonitor = performanceMonitor;
            _eventLogger = eventLogger;
            _printerService = printerService;
            _displayService = displayService;
            _callSystemService = callSystemService;
            _logger = logger;
        }

        /// <summary>
        /// Kiểm tra sức khỏe hệ thống
        /// GET /api/diagnostics/health
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                var result = await _healthCheckService.PerformHealthCheckAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Test từng component
        /// POST /api/diagnostics/test-component
        /// </summary>
        [HttpPost("test-component")]
        public async Task<IActionResult> TestComponent([FromQuery] string component)
        {
            try
            {
                var result = new { component, status = "", message = "" };

                return component?.ToLower() switch
                {
                    "printer" => Ok(new
                    {
                        component = "Printer",
                        status = await _printerService.IsPrinterReadyAsync() ? "OK" : "FAILED",
                        printerStatus = await _printerService.GetPrinterStatusAsync()
                    }),

                    "display" => Ok(new
                    {
                        component = "Display",
                        status = await _displayService.DisplayMessageAsync("TEST MESSAGE") ? "OK" : "FAILED",
                        message = "Test message sent"
                    }),

                    "call" => Ok(new
                    {
                        component = "Call System",
                        status = await _callSystemService.GetCallSystemStatusAsync() == 1 ? "OK" : "FAILED"
                    }),

                    _ => BadRequest(new { error = "Unknown component" })
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// In phiếu test
        /// POST /api/diagnostics/test-print
        /// </summary>
        [HttpPost("test-print")]
        public async Task<IActionResult> TestPrint()
        {
            try
            {
                var testCommand = new PrintCommand
                {
                    TicketNumber = $"TEST-{DateTime.UtcNow:HHmmss}",
                    DepartmentName = "Phòng Khám Test",
                    QueuePosition = 1,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _printerService.PrintTicketAsync(testCommand);
                return Ok(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Xem các event gần đây
        /// GET /api/diagnostics/recent-events
        /// </summary>
        [HttpGet("recent-events")]
        public async Task<IActionResult> GetRecentEvents([FromQuery] int count = 50)
        {
            try
            {
                var events = await _eventLogger.GetRecentEventsAsync(count);
                return Ok(new { total = events.Count, events });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Xuất logs ra file
        /// GET /api/diagnostics/export-logs
        /// </summary>
        [HttpGet("export-logs")]
        public async Task<IActionResult> ExportLogs()
        {
            try
            {
                var fileName = $"device-logs-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.csv";
                var filePath = Path.Combine(Path.GetTempPath(), fileName);

                await _eventLogger.ExportEventsAsync(filePath);

                var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin hiệu suất
        /// GET /api/diagnostics/performance
        /// </summary>
        [HttpGet("performance")]
        public async Task<IActionResult> GetPerformanceStats()
        {
            try
            {
                var stats = await _performanceMonitor.GetStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}