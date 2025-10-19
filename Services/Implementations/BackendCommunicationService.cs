using System;
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

        public BackendCommunicationService(ILogger<BackendCommunicationService> logger, IHttpClientFactory httpFactory, IConfiguration config)
        {
            _logger = logger;
            _httpClient = httpFactory.CreateClient("BackendApi");
            _backendUrl = config.GetValue<string>("Backend:BaseUrl", "http://localhost:5000");
            _deviceId = config.GetValue<string>("Device:DeviceId", "KIOSK-001");
        }

        public async Task<DeviceCommandResponse> GetPendingCommandsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_backendUrl}/api/device/commands/{_deviceId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<DeviceCommandResponse>(json);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Get commands error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SendHeartbeatAsync(DeviceStatusDto status)
        {
            try
            {
                var json = JsonConvert.SerializeObject(status);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_backendUrl}/api/device/heartbeat", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Heartbeat error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ReportErrorAsync(string errorMessage, string errorType)
        {
            try
            {
                var errorData = new { DeviceId = _deviceId, Message = errorMessage, Type = errorType, Timestamp = DateTime.UtcNow };
                var json = JsonConvert.SerializeObject(errorData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_backendUrl}/api/device/errors", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> AcknowledgeCommandAsync(string commandId)
        {
            try
            {
                var ackData = new { DeviceId = _deviceId, CommandId = commandId, Status = "COMPLETED" };
                var json = JsonConvert.SerializeObject(ackData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_backendUrl}/api/device/commands/acknowledge", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
