using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KioskDevice.Models;
using KioskDevice.Services.Interfaces;

namespace KioskDevice.Services.Implementations
{
    public class MockBackendCommunicationService : IBackendCommunicationService
    {
        private readonly ILogger<MockBackendCommunicationService> _logger;

        public MockBackendCommunicationService(ILogger<MockBackendCommunicationService> logger)
        {
            _logger = logger;
        }

        public async Task<ApiResponse<CommandData>> GetPendingCommandsAsync()
        {
            _logger.LogInformation("Mock: No pending commands");
            return await Task.FromResult<ApiResponse<CommandData>>(null);
        }

        public async Task<ApiResponse<object>> SendHeartbeatAsync(HeartbeatData heartbeatData)
        {
            _logger.LogInformation("Mock: Heartbeat sent");
            var response = new ApiResponse<object>
            {
                CommandId = Guid.NewGuid().ToString(),
                DeviceId = "KIOSK-001",
                Timestamp = DateTime.UtcNow,
                Type = "HEARTBEAT",
                Message = "Đã nhận HEARTBEAT",
                Status = true,
                Data = new { }
            };
            return await Task.FromResult(response);
        }

        public async Task<ApiResponse<object>> SendCommandResultAsync(string commandId, bool success, string message = "")
        {
            _logger.LogInformation($"Mock: Command result - {commandId}: {success}");
            var response = new ApiResponse<object>
            {
                CommandId = commandId,
                DeviceId = "KIOSK-001",
                Timestamp = DateTime.UtcNow,
                Type = "RESULT",
                Message = "Đã nhận kết quả",
                Status = true,
                Data = new { }
            };
            return await Task.FromResult(response);
        }

        public async Task<ApiResponse<object>> ReportErrorAsync(string commandId, string errorType, string message)
        {
            _logger.LogWarning($"Mock: Error reported - {errorType}: {message}");
            var response = new ApiResponse<object>
            {
                CommandId = commandId,
                DeviceId = "KIOSK-001",
                Timestamp = DateTime.UtcNow,
                Type = errorType,
                Message = "Đã nhận kết quả",
                Status = true,
                Data = new { }
            };
            return await Task.FromResult(response);
        }
    }
}
