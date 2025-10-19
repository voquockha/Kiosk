namespace KioskDevice.Controllers
{
    using KioskDevice.Models;
    using KioskDevice.Services.Advanced;
    using KioskDevice.Services.Interfaces;
    using Microsoft.AspNetCore.Mvc;

    // ========== 1. DEVICE COMMAND CONTROLLER ==========
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceOrchestrator _orchestrator;
        private readonly IDeviceStateManager _stateManager;
        private readonly IEventLogger _eventLogger;
        private readonly ILogger<DeviceController> _logger;

        public DeviceController(
            IDeviceOrchestrator orchestrator,
            IDeviceStateManager stateManager,
            IEventLogger eventLogger,
            ILogger<DeviceController> logger)
        {
            _orchestrator = orchestrator;
            _stateManager = stateManager;
            _eventLogger = eventLogger;
            _logger = logger;
        }

        /// <summary>
        /// In phiếu số thứ tự
        /// POST /api/device/print
        /// </summary>
        [HttpPost("print")]
        public async Task<IActionResult> PrintTicket([FromBody] PrintCommand command)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var canProcess = await _stateManager.CanProcessCommandAsync("PRINT");
                if (!canProcess)
                    return StatusCode(503, new { message = "Device not ready for printing" });

                await _orchestrator.ProcessPrintCommandAsync(command);

                await _eventLogger.LogEventAsync(new DeviceEvent
                {
                    Type = EventType.PrintStarted,
                    TicketNumber = command.TicketNumber,
                    Description = $"Print started for ticket {command.TicketNumber}"
                });

                return Ok(new { success = true, message = "Print command processed", ticket = command.TicketNumber });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Print error: {ex.Message}");
                await _eventLogger.LogEventAsync(new DeviceEvent
                {
                    Type = EventType.PrintFailed,
                    TicketNumber = command.TicketNumber,
                    Description = ex.Message
                });
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Gọi số thứ tự
        /// POST /api/device/call
        /// </summary>
        [HttpPost("call")]
        public async Task<IActionResult> CallTicket([FromBody] CallCommand command)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var canProcess = await _stateManager.CanProcessCommandAsync("CALL");
                if (!canProcess)
                    return StatusCode(503, new { message = "Device not ready for calling" });

                await _orchestrator.ProcessCallCommandAsync(command);

                await _eventLogger.LogEventAsync(new DeviceEvent
                {
                    Type = EventType.CallStarted,
                    TicketNumber = command.TicketNumber,
                    Description = $"Called ticket {command.TicketNumber} to counter {command.CounterNumber}",
                    Metadata = new Dictionary<string, object> { { "counter", command.CounterNumber } }
                });

                return Ok(new { success = true, message = "Call command processed", ticket = command.TicketNumber });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Call error: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy trạng thái device
        /// GET /api/device/status
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var status = await _orchestrator.GetDeviceStatusAsync();
                status.Status = _stateManager.GetCurrentState().ToString();
                return Ok(status);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Reset device
        /// POST /api/device/reset
        /// </summary>
        [HttpPost("reset")]
        public async Task<IActionResult> ResetDevice()
        {
            try
            {
                await _stateManager.ChangeStateAsync(DeviceState.Initializing, "Reset requested");
                await Task.Delay(2000); // Mô phỏng reset time
                await _stateManager.ChangeStateAsync(DeviceState.Ready, "Reset completed");

                await _eventLogger.LogEventAsync(new DeviceEvent
                {
                    Type = EventType.DeviceOnline,
                    Description = "Device reset completed"
                });

                return Ok(new { success = true, message = "Device reset completed" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}