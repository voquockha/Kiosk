using System;
using System.Text;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using KioskDevice.Services.Interfaces;

namespace KioskDevice.Services.Implementations
{
    public class DisplayService : IDisplayService
    {
        private readonly ILogger<DisplayService> _logger;
        private readonly IConfiguration _config;


        public DisplayService(ILogger<DisplayService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }


        public async Task<bool> DisplayMessageAsync(string ticketNumber, string counterNumber)
        {
            try
            {
                // await SendToDisplayAsync($"*[H1]QUAY {counterNumber}[!]", counterNumber);
                // await Task.Delay(200); // delay nhẹ để LED xử lý
                await SendToDisplayAsync($"*[H2]MOI SO {ticketNumber}[!]", counterNumber);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Display error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendToDisplayAsync(string message, string counterNumber)
        {
            try
            {
                // Đọc IP và port từ file cấu hình (ví dụ Devices:DisplayMappings:1:Host)
                string host = _config[$"Devices:DisplayMappings:{counterNumber}:Host"];
                int port = _config.GetValue<int>($"Devices:DisplayMappings:{counterNumber}:Port", 80);

                if (string.IsNullOrEmpty(host))
                {
                    _logger.LogWarning($"Not found IP DisplayMappings for department {counterNumber}");
                    return false;
                }

                byte[] data = Encoding.ASCII.GetBytes(message);

                using (TcpClient client = new TcpClient())
                {
                    // Thử kết nối với timeout (2 giây)
                    var connectTask = client.ConnectAsync(host, port);
                    var timeoutTask = Task.Delay(1000);

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        _logger.LogWarning($"Timeout connect to LED {host}:{port} (QUAY {counterNumber})");
                        return false;
                    }

                    using (NetworkStream stream = client.GetStream())
                    {
                        await stream.WriteAsync(data, 0, data.Length);
                        await stream.FlushAsync();
                    }
                }

                _logger.LogInformation($"Send {host}:{port} (QUAY {counterNumber}) → {message}");
                await Task.Delay(200); // delay nhẹ để LED xử lý
                return true;
            }
            catch (SocketException ex)
            {
                _logger.LogWarning($"Can't not connected to LED {counterNumber} ({ex.SocketErrorCode}) - {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                _logger.LogWarning($"Send data to led Error {counterNumber}: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"SendToDisplayAsync error (QUAY {counterNumber}): {ex.Message}");
                return false;
            }
        }


    }
}
