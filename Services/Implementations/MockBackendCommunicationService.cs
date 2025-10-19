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

        public async Task<DeviceCommandResponse> GetPendingCommandsAsync()
        {
            // Không trả về lệnh nào - Device chỉ nghe thôi
            _logger.LogInformation("Mock: No pending commands");
            return null;
        }

        public async Task<bool> SendHeartbeatAsync(DeviceStatusDto status)
        {
            _logger.LogInformation($"Mock: Heartbeat sent - Status: {status.Status}");
            return true;
        }

        public async Task<bool> ReportErrorAsync(string errorMessage, string errorType)
        {
            _logger.LogWarning($"Mock: Error reported - {errorType}: {errorMessage}");
            return true;
        }

        public async Task<bool> AcknowledgeCommandAsync(string commandId)
        {
            _logger.LogInformation($"Mock: Command acknowledged - {commandId}");
            return true;
        }
    }
}