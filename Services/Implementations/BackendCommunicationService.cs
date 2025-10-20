using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using KioskDevice.Models;
using KioskDevice.Services.Interfaces;

namespace KioskDevice.Services.Implementations
{
    public class BackendCommunicationService : IBackendCommunicationService
    {
        private readonly ILogger<BackendCommunicationService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _backendUrl;
        private readonly string _deviceId;

        public BackendCommunicationService(
            ILogger<BackendCommunicationService> logger,
            IHttpClientFactory httpFactory,
            IConfiguration config)
        {
            _logger = logger;
            _httpClient = httpFactory.CreateClient("BackendApi");
            _backendUrl = config.GetValue<string>("Backend:BaseUrl", "http://localhost:5000");
            _deviceId = config.GetValue<string>("Device:DeviceId", "KIOSK-001");
        }

        public async Task<ApiResponse<CommandData>> GetPendingCommandsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"{_backendUrl}/api/v1/kiosks/commands/{_deviceId}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ApiResponse<CommandData>>(json);
                }

                _logger.LogWarning($"Failed to get commands: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Get commands error: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<object>> SendHeartbeatAsync(HeartbeatData heartbeatData)
        {
            try
            {
                var request = new HeartbeatRequest
                {
                    CommandId = Guid.NewGuid().ToString(),
                    DeviceId = _deviceId,
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.0",
                    Message = "Thông tin heartbeat",
                    Type = "HEARTBEAT",
                    Data = heartbeatData
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_backendUrl}/api/v1/kiosks/heartbeat",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ApiResponse<object>>(responseJson);
                }

                _logger.LogWarning($"Heartbeat failed: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Heartbeat error: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<object>> SendCommandResultAsync(string commandId, bool success, string message = "")
        {
            try
            {
                var request = new CommandResultRequest
                {
                    CommandId = commandId,
                    DeviceId = _deviceId,
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.0",
                    Message = message,
                    Type = "RESULT",
                    Data = new { status = success }
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_backendUrl}/api/v1/kiosks/commands/result",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ApiResponse<object>>(responseJson);
                }

                _logger.LogWarning($"Command result failed: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Send command result error: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<object>> ReportErrorAsync(string commandId, string errorType, string message)
        {
            try
            {
                var request = new ErrorReportRequest
                {
                    CommandId = commandId,
                    DeviceId = _deviceId,
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.0",
                    Message = message,
                    Type = errorType,
                    Data = new { }
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_backendUrl}/api/v1/kiosks/errors",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ApiResponse<object>>(responseJson);
                }

                _logger.LogWarning($"Error report failed: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Report error error: {ex.Message}");
                return null;
            }
        }
    }
}
