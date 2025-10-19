using System;
using System.IO.Ports;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using KioskDevice.Services.Interfaces;

namespace KioskDevice.Services.Implementations
{
    public class DisplayService : IDisplayService
    {
        private readonly ILogger<DisplayService> _logger;
        private readonly string _displayComPort;

        public DisplayService(ILogger<DisplayService> logger, IConfiguration config)
        {
            _logger = logger;
            _displayComPort = config.GetValue<string>("Devices:DisplayComPort", "COM3");
        }

        public async Task<bool> DisplayTicketAsync(string ticketNumber, string department, int position)
        {
            try
            {
                var message = $"SỐ THỨ TỰ: {ticketNumber}\nKHOA: {department}\nVỊ TRÍ: {position}";
                return await SendToDisplayAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Display error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ClearDisplayAsync()
        {
            try
            {
                return await SendToDisplayAsync("\x0C"); // Form feed command
            }
            catch { return false; }
        }

        public async Task<bool> DisplayMessageAsync(string message)
        {
            return await SendToDisplayAsync(message);
        }

        private async Task<bool> SendToDisplayAsync(string message)
        {
            try
            {
                using (var port = new SerialPort(_displayComPort, 9600))
                {
                    if (port.IsOpen) port.Close();
                    port.Open();
                    port.WriteLine(message);
                    port.Close();
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
