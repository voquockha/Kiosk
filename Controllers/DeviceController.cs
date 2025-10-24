using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using KioskDevice.Models;
using KioskDevice.Services.Advanced;
using KioskDevice.Services.Interfaces;

namespace KioskDevice.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceOrchestrator _orchestrator;
        private readonly IDeviceStateManager _stateManager;
        private readonly IEventLogger _eventLogger;
        private readonly IBackendCommunicationService _backendService;
        private readonly ILogger<DeviceController> _logger;
        private readonly IConfiguration _config;

        public DeviceController(
            IDeviceOrchestrator orchestrator,
            IDeviceStateManager stateManager,
            IEventLogger eventLogger,
            IBackendCommunicationService backendService,
            ILogger<DeviceController> logger,
            IConfiguration config)
        {
            _orchestrator = orchestrator;
            _stateManager = stateManager;
            _eventLogger = eventLogger;
            _backendService = backendService;
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// In phiếu số thứ tự
        /// POST /api/device/print
        /// </summary>
        [HttpPost("print")]
        public async Task<IActionResult> PrintTicket([FromBody] ApiResponse<CommandData> request)
        {
            if (request?.Data == null)
                return BadRequest(new PrintResponse
                {
                    Status = false,
                    Message = "Invalid request data",
                    Type = "PRINT"
                });

            try
            {
                // Kiểm tra device sẵn sàng
                var canProcess = await _stateManager.CanProcessCommandAsync("PRINT");
                if (!canProcess)
                {
                    _logger.LogWarning($"Device not ready to print. Current state: {_stateManager.GetCurrentState()}");
                    return StatusCode(503, new PrintResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Status = false,
                        Message = "Device not ready for printing",
                        Type = "PRINT",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Khóa device - từ chối lệnh mới
                await _stateManager.ChangeStateAsync(DeviceState.Printing, "Processing print command");

                try
                {
                    _logger.LogInformation($"Processing print command for ticket: {request.Data.TicketNumber}");

                    // Tạo PrintCommand từ request data
                    var printCommand = new PrintCommand
                    {
                        TicketNumber = request.Data?.TicketNumber,
                        DepartmentName = request.Data?.DepartmentName,
                        CounterNumber = request.Data?.CounterNumber,
                        FilePath = request.Data?.Path,
                    };

                    // Xử lý in phiếu
                    await _orchestrator.ProcessPrintCommandAsync(printCommand);

                    // Ghi log sự kiện
                    await _eventLogger.LogEventAsync(new DeviceEvent
                    {
                        Type = EventType.PrintCompleted,
                        TicketNumber = request.Data.TicketNumber,
                        Description = $"Print completed for ticket {request.Data.TicketNumber}",
                        Metadata = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "commandId", request.CommandId },
                            { "department", request.Data.DepartmentName }
                        }
                    });

                    // Báo cáo kết quả về Backend
                    await _backendService.SendCommandResultAsync(
                        request.CommandId,
                        true,
                        $"Đã in phiếu {request.Data.TicketNumber}"
                    );

                    // Trả lại Ready
                    await _stateManager.ChangeStateAsync(DeviceState.Ready, "Print completed");

                    return Ok(new PrintResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Timestamp = DateTime.UtcNow,
                        Type = "PRINT",
                        Message = "Đã in",
                        Status = true,
                        Data = new PrintResponseData { TicketNumber = request.Data.TicketNumber }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Print processing error: {ex.Message}");

                    // Báo lỗi về Backend
                    await _backendService.ReportErrorAsync(
                        request.CommandId,
                        "PRINTER_ERROR",
                        $"Print failed: {ex.Message}"
                    );

                    // Ghi log sự kiện lỗi
                    await _eventLogger.LogEventAsync(new DeviceEvent
                    {
                        Type = EventType.PrintFailed,
                        TicketNumber = request.Data.TicketNumber,
                        Description = $"Print failed: {ex.Message}",
                        Metadata = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "commandId", request.CommandId },
                            { "error", ex.Message }
                        }
                    });

                    // Set state Error
                    await _stateManager.ChangeStateAsync(DeviceState.Error, ex.Message);

                    return StatusCode(500, new PrintResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Timestamp = DateTime.UtcNow,
                        Type = "PRINT",
                        Message = $"Print failed: {ex.Message}",
                        Status = false
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error in PrintTicket: {ex.Message}");
                return StatusCode(500, new PrintResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Type = "PRINT",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Gọi số thứ tự
        /// POST /api/device/call
        /// </summary>
        [HttpPost("call")]
        public async Task<IActionResult> CallTicket([FromBody] ApiResponse<CommandData> request)
        {
            if (request?.Data == null)
                return BadRequest(new CallResponse
                {
                    Status = false,
                    Message = "Invalid request data",
                    Type = "CALL"
                });

            try
            {
                // Kiểm tra device sẵn sàng
                var canProcess = await _stateManager.CanProcessCommandAsync("CALL");
                if (!canProcess)
                {
                    _logger.LogWarning($"Device not ready to call. Current state: {_stateManager.GetCurrentState()}");
                    return StatusCode(503, new CallResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Status = false,
                        Message = "Device not ready for calling",
                        Type = "CALL",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Khóa device - từ chối lệnh mới
                await _stateManager.ChangeStateAsync(DeviceState.Calling, "Processing call command");

                try
                {
                    _logger.LogInformation($"Processing call command for ticket: {request.Data.TicketNumber}");

                    // Tạo CallCommand từ request data
                    var callCommand = new CallCommand
                    {
                        TicketNumber = request.Data.TicketNumber,
                        DepartmentName = request.Data.DepartmentName,
                        CounterNumber = request.Data.CounterNumber,
                        Status = "CALLING",
                        AudioPath = request.Data.Path
                    };

                    // Xử lý gọi số
                    await _orchestrator.ProcessCallCommandAsync(callCommand);

                    // Ghi log sự kiện
                    await _eventLogger.LogEventAsync(new DeviceEvent
                    {
                        Type = EventType.CallCompleted,
                        TicketNumber = request.Data.TicketNumber,
                        Description = $"Called ticket {request.Data.TicketNumber} to counter {request.Data.CounterNumber}",
                        Metadata = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "commandId", request.CommandId },
                            { "counter", request.Data.CounterNumber }
                        }
                    });

                    // Báo cáo kết quả về Backend
                    await _backendService.SendCommandResultAsync(
                        request.CommandId,
                        true,
                        $"Called ticket {request.Data.TicketNumber} department {request.Data.CounterNumber}"
                    );

                    // Trả lại Ready
                    await _stateManager.ChangeStateAsync(DeviceState.Ready, "Call completed");

                    return Ok(new CallResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Timestamp = DateTime.UtcNow,
                        Type = "CALL",
                        Message = "Đã call",
                        Status = true,
                        Data = new CallResponseData { TicketNumber = request.Data.TicketNumber }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Call processing error: {ex.Message}");

                    // Báo lỗi về Backend
                    await _backendService.ReportErrorAsync(
                        request.CommandId,
                        "CALL_SYSTEM_ERROR",
                        $"Call failed: {ex.Message}"
                    );

                    // Ghi log sự kiện lỗi
                    await _eventLogger.LogEventAsync(new DeviceEvent
                    {
                        Type = EventType.CallMissed,
                        TicketNumber = request.Data.TicketNumber,
                        Description = $"Call failed: {ex.Message}",
                        Metadata = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "commandId", request.CommandId },
                            { "error", ex.Message }
                        }
                    });

                    // Set state Error
                    await _stateManager.ChangeStateAsync(DeviceState.Error, ex.Message);

                    return StatusCode(500, new CallResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Timestamp = DateTime.UtcNow,
                        Type = "CALL",
                        Message = $"Call failed: {ex.Message}",
                        Status = false
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error in CallTicket: {ex.Message}");
                return StatusCode(500, new CallResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Type = "CALL",
                    Timestamp = DateTime.UtcNow
                });
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
                var heartbeatData = ConvertStatusToHeartbeatData(status);

                var response = new ApiResponse<HeartbeatData>
                {
                    CommandId = Guid.NewGuid().ToString(),
                    DeviceId = _config.GetValue<string>("Device:DeviceId", "KIOSK-001"),
                    Timestamp = DateTime.UtcNow,
                    Type = "HEARTBEAT",
                    Message = "Thông tin heartbeat",
                    Status = true,
                    Data = heartbeatData
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Get status error: {ex.Message}");
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
                await Task.Delay(2000);
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

        private HeartbeatData ConvertStatusToHeartbeatData(DeviceStatusDto status)
        {
            return new HeartbeatData
            {
                Speaker = new DeviceInfo
                {
                    Name = _config.GetValue<string>("Devices:AudioName", "AudioName"),
                    Id =  _config.GetValue<string>("Devices:AudioID", "AudioID"),
                    Type = "audio",
                    Status = status.CallSystemStatus == 1 ? "ok" : "error",
                    Volume = 80,
                    IsPlaying = false
                },
                Printer = new DeviceInfo
                {
                    Name = _config.GetValue<string>("Devices:PrinterName", "PrinterName"),
                    Id = _config.GetValue<string>("Devices:PrinterID", "PrinterID "),
                    Type = "thermal_printer",
                    Status = status.PrinterStatus == "Ready" ? "ok" : "error",
                    Paper = status.PrinterStatus == "Ready" ? "ok" : "error",
                    Temp = "normal",
                    IsPrinting = false
                }
            };
        }
    }
}
