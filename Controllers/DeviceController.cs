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
        private readonly IPrinterService _printerService;
        private readonly ICallSystemService _callSystemService;

        public DeviceController(
            IDeviceOrchestrator orchestrator,
            IDeviceStateManager stateManager,
            IEventLogger eventLogger,
            IBackendCommunicationService backendService,
            ILogger<DeviceController> logger,
            IConfiguration config,
            IPrinterService printerService,
            ICallSystemService callSystemService)
        {
            _orchestrator = orchestrator;
            _stateManager = stateManager;
            _eventLogger = eventLogger;
            _backendService = backendService;
            _logger = logger;
            _config = config;
            _printerService = printerService;
            _callSystemService = callSystemService;
        }

        /// <summary>
        /// In phiếu số thứ tự
        /// POST /api/device/print
        /// </summary>
        [HttpPost("print")]
        public async Task<IActionResult> PrintTicket([FromBody] ApiResponse<CommandData> request)
        {
            if (request?.Data == null)
                return BadRequest(new PrintResponse { Status = false, Message = "Invalid data", Type = "PRINT" });

            try
            {
                // 1. Kiểm tra SPAM (đang xử lý lệnh PRINT khác)
                var canProcess = await _stateManager.CanProcessCommandAsync("PRINT");
                if (!canProcess)
                {
                    _logger.LogWarning($"Print rejected: Device is busy printing");
                    return StatusCode(429, new PrintResponse // 429 Too Many Requests
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Status = false,
                        Message = "Device đang xử lý lệnh in khác, vui lòng chờ",
                        Type = "PRINT",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // 2. Kiểm tra máy in SẴN SÀNG chưa (kiểm tra thực tế)
                var printerReady = await _printerService.IsPrinterReadyAsync();
                if (!printerReady)
                {
                    var printerStatus = await _printerService.GetPrinterStatusAsync();
                    _logger.LogWarning($"Printer not ready: {printerStatus}");
                    
                    // Báo lỗi về Backend nhưng KHÔNG khóa state
                    await _backendService.ReportErrorAsync(
                        request.CommandId,
                        "PRINTER_ERROR",
                        $"Máy in không sẵn sàng: {printerStatus}"
                    );

                    return StatusCode(503, new PrintResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Status = false,
                        Message = $"Máy in không sẵn sàng: {printerStatus}",
                        Type = "PRINT",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // 3. Khóa tạm thời (chống spam)
                await _stateManager.ChangeStateAsync(DeviceState.Printing, "Processing print");

                try
                {
                    _logger.LogInformation($"Printing ticket: {request.Data.TicketNumber}");

                    var printCommand = new PrintCommand
                    {
                        TicketNumber = request.Data?.TicketNumber,
                        DepartmentName = request.Data?.DepartmentName,
                        CounterNumber = request.Data?.CounterNumber,
                        FilePath = request.Data?.Path
                    };

                    // 4. In phiếu
                    await _orchestrator.ProcessPrintCommandAsync(printCommand);

                    // 5. Ghi log thành công
                    await _eventLogger.LogEventAsync(new DeviceEvent
                    {
                        Type = EventType.PrintCompleted,
                        TicketNumber = request.Data.TicketNumber,
                        Description = $"Printed: {request.Data.TicketNumber}",
                        Metadata = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "commandId", request.CommandId }
                        }
                    });

                    // 6. Báo Backend thành công
                    await _backendService.SendCommandResultAsync(
                        request.CommandId,
                        true,
                        $"Đã in phiếu {request.Data.TicketNumber}"
                    );

                    // 7. Mở khóa ngay
                    await _stateManager.ChangeStateAsync(DeviceState.Ready, "Print completed");

                    return Ok(new PrintResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Timestamp = DateTime.UtcNow,
                        Type = "PRINT",
                        Message = "Đã in thành công",
                        Status = true,
                        Data = new PrintResponseData { TicketNumber = request.Data.TicketNumber }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Print failed: {ex.Message}");

                    // Báo lỗi
                    await _backendService.ReportErrorAsync(
                        request.CommandId,
                        "PRINTER_ERROR",
                        $"In thất bại: {ex.Message}"
                    );

                    await _eventLogger.LogEventAsync(new DeviceEvent
                    {
                        Type = EventType.PrintFailed,
                        TicketNumber = request.Data.TicketNumber,
                        Description = $"Print failed: {ex.Message}"
                    });

                    // Mở khóa ngay (cho phép thử lại)
                    await _stateManager.ChangeStateAsync(DeviceState.Ready, "Print failed, ready for retry");

                    return StatusCode(500, new PrintResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Timestamp = DateTime.UtcNow,
                        Type = "PRINT",
                        Message = $"In thất bại: {ex.Message}",
                        Status = false
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error: {ex.Message}");
                return StatusCode(500, new PrintResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Type = "PRINT",
                    Timestamp = DateTime.UtcNow
                });
            }
        }


        // ============================================
        // FILE: Controllers/DeviceController.cs (FLEXIBLE - Call)
        // ============================================
        [HttpPost("call")]
        public async Task<IActionResult> CallTicket([FromBody] ApiResponse<CommandData> request)
        {
            if (request?.Data == null)
                return BadRequest(new CallResponse { Status = false, Message = "Invalid data", Type = "CALL" });

            try
            {
                // 1. Kiểm tra SPAM
                var canProcess = await _stateManager.CanProcessCommandAsync("CALL");
                if (!canProcess)
                {
                    _logger.LogWarning($"Call rejected: Device is busy calling");
                    return StatusCode(429, new CallResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Status = false,
                        Message = "Device đang xử lý lệnh gọi khác, vui lòng chờ",
                        Type = "CALL",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // 2. Kiểm tra hệ thống gọi SẴN SÀNG chưa
                var callSystemStatus = await _callSystemService.GetCallSystemStatusAsync();
                if (callSystemStatus != 1)
                {
                    _logger.LogWarning($"Call system not ready: status={callSystemStatus}");
                    
                    await _backendService.ReportErrorAsync(
                        request.CommandId,
                        "CALL_SYSTEM_ERROR",
                        $"Hệ thống gọi không sẵn sàng"
                    );

                    return StatusCode(503, new CallResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Status = false,
                        Message = "Hệ thống gọi không sẵn sàng",
                        Type = "CALL",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // 3. Khóa tạm thời
                await _stateManager.ChangeStateAsync(DeviceState.Calling, "Processing call");

                try
                {
                    _logger.LogInformation($"Calling ticket: {request.Data.TicketNumber}");

                    var callCommand = new CallCommand
                    {
                        TicketNumber = request.Data.TicketNumber,
                        DepartmentName = request.Data.DepartmentName,
                        CounterNumber = request.Data.CounterNumber,
                        Status = "CALLING",
                        AudioPath = request.Data.Path
                    };

                    // 4. Gọi số
                    await _orchestrator.ProcessCallCommandAsync(callCommand);

                    // 5. Ghi log
                    await _eventLogger.LogEventAsync(new DeviceEvent
                    {
                        Type = EventType.CallCompleted,
                        TicketNumber = request.Data.TicketNumber,
                        Description = $"Called: {request.Data.TicketNumber} to counter {request.Data.CounterNumber}"
                    });

                    // 6. Báo Backend
                    await _backendService.SendCommandResultAsync(
                        request.CommandId,
                        true,
                        $"Đã gọi phiếu {request.Data.TicketNumber}"
                    );

                    // 7. Mở khóa
                    await _stateManager.ChangeStateAsync(DeviceState.Ready, "Call completed");

                    return Ok(new CallResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Timestamp = DateTime.UtcNow,
                        Type = "CALL",
                        Message = "Đã gọi thành công",
                        Status = true,
                        Data = new CallResponseData { TicketNumber = request.Data.TicketNumber }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Call failed: {ex.Message}");

                    await _backendService.ReportErrorAsync(
                        request.CommandId,
                        "CALL_SYSTEM_ERROR",
                        $"Gọi thất bại: {ex.Message}"
                    );

                    await _eventLogger.LogEventAsync(new DeviceEvent
                    {
                        Type = EventType.CallMissed,
                        TicketNumber = request.Data.TicketNumber,
                        Description = $"Call failed: {ex.Message}"
                    });

                    // Mở khóa ngay
                    await _stateManager.ChangeStateAsync(DeviceState.Ready, "Call failed, ready for retry");

                    return StatusCode(500, new CallResponse
                    {
                        CommandId = request.CommandId,
                        DeviceId = request.DeviceId,
                        Timestamp = DateTime.UtcNow,
                        Type = "CALL",
                        Message = $"Gọi thất bại: {ex.Message}",
                        Status = false
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error: {ex.Message}");
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
