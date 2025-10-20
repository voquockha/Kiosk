namespace KioskDevice.Services.Advanced
{
    using KioskDevice.Models;
    using KioskDevice.Services.Interfaces;
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging;
    using System.Threading;
    public interface IHealthCheckService
    {
        Task<HealthCheckResult> PerformHealthCheckAsync();
    }

    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; }
        public Dictionary<string, ComponentHealth> Components { get; set; }
        public DateTime CheckedAt { get; set; }
    }

    public class ComponentHealth
    {
        public string Name { get; set; }
        public bool IsHealthy { get; set; }
        public string Status { get; set; }
        public string LastError { get; set; }
    }

   public class HealthCheckService : IHealthCheckService
{
    private readonly IPrinterService _printerService;
    private readonly IDisplayService _displayService;
    private readonly ICallSystemService _callSystemService;
    private readonly IBackendCommunicationService _backendService;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(
        IPrinterService printerService,
        IDisplayService displayService,
        ICallSystemService callSystemService,
        IBackendCommunicationService backendService,
        ILogger<HealthCheckService> logger)
    {
        _printerService = printerService;
        _displayService = displayService;
        _callSystemService = callSystemService;
        _backendService = backendService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> PerformHealthCheckAsync()
    {
        var result = new HealthCheckResult
        {
            Components = new Dictionary<string, ComponentHealth>(),
            CheckedAt = DateTime.UtcNow
        };

        try
        {
            // Kiểm tra Printer
            var printerReady = await _printerService.IsPrinterReadyAsync();
            var printerStatus = await _printerService.GetPrinterStatusAsync();

            result.Components["Printer"] = new ComponentHealth
            {
                Name = "Printer",
                IsHealthy = printerReady,
                Status = printerStatus
            };

            // Kiểm tra Call System
            var callStatus = await _callSystemService.GetCallSystemStatusAsync();
            result.Components["CallSystem"] = new ComponentHealth
            {
                Name = "Call System",
                IsHealthy = callStatus == 1,
                Status = callStatus == 1 ? "Running" : "Error"
            };

            // Kiểm tra Display
            result.Components["Display"] = new ComponentHealth
            {
                Name = "Display",
                IsHealthy = true,
                Status = "OK"
            };

            // Chuẩn bị HeartbeatData mới
            var heartbeat = new HeartbeatData
            {
                Speaker = new DeviceInfo
                {
                    Name = "Speaker",
                    Id = "SPEAKER_01",
                    Type = "Audio",
                    Status = "Online",
                    Volume = 80,
                    IsPlaying = false
                },
                Printer = new DeviceInfo
                {
                    Name = "Printer",
                    Id = "PRINTER_01",
                    Type = "Thermal",
                    Status = printerReady ? "Online" : "Offline",
                    Paper = printerStatus,
                    IsPrinting = false
                }
            };

            // Gửi heartbeat tới backend
            try
            {
                var response = await _backendService.SendHeartbeatAsync(heartbeat);
                var backendOk = response.Status;
                result.Components["Backend"] = new ComponentHealth
                {
                    Name = "Backend",
                    IsHealthy = backendOk,
                    Status = backendOk ? "Connected" : "Disconnected"
                };
            }
            catch (Exception ex)
            {
                result.Components["Backend"] = new ComponentHealth
                {
                    Name = "Backend",
                    IsHealthy = false,
                    Status = "Error",
                    LastError = ex.Message
                };
            }

            // Tổng hợp kết quả
            result.IsHealthy = result.Components.Values.All(c => c.IsHealthy);
            _logger.LogInformation($"Health check completed. Overall health: {(result.IsHealthy ? "HEALTHY" : "UNHEALTHY")}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Health check failed: {ex.Message}");
            result.IsHealthy = false;
        }

        return result;
    }
}

}