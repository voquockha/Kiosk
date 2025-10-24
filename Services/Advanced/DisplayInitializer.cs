using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using KioskDevice.Services.Interfaces;

namespace KioskDevice.Services.Advanced
{
    public class DisplayInitializer : IHostedService
    {
        private readonly IDisplayService _displayService;
        private readonly IConfiguration _config;
        private readonly ILogger<DisplayInitializer> _logger;

        public DisplayInitializer(IDisplayService displayService, IConfiguration config, ILogger<DisplayInitializer> logger)
        {
            _displayService = displayService;
            _config = config;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Đọc danh sách counter từ cấu hình
                var section = _config.GetSection("Devices:DisplayMappings");
                foreach (var child in section.GetChildren())
                {
                    var counterNumber = child.Key;
                    _logger.LogInformation($"LED init {counterNumber}...");

                    // Gửi chuỗi lệnh cấu hình LED

                    await _displayService.SendToDisplayAsync($"*[Matrix][SetFont]Font2[!]", counterNumber);
                    await Task.Delay(200);
                    await _displayService.SendToDisplayAsync($"*[Matrix][SetAlign][H1]Center[!]", counterNumber);
                    await Task.Delay(200);
                    await _displayService.SendToDisplayAsync($"*[Matrix][SetAlign][H2]Center[!]", counterNumber);
                    await Task.Delay(200);
                    await _displayService.SendToDisplayAsync($"*[H1]QUAY {counterNumber}[!]", counterNumber);
                    await Task.Delay(200);
                    await _displayService.SendToDisplayAsync($"*[H2][!]", counterNumber);

                    _logger.LogInformation($"LED QUAY {counterNumber} READY");
                }

                _logger.LogInformation("=== Init Display success ===");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Init Display error: {ex.Message}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DisplayInitializer stop");
            return Task.CompletedTask;
        }
    }
}
