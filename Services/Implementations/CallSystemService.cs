using System;
using System.IO.Ports;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Media;
using KioskDevice.Models;
using KioskDevice.Services.Interfaces;

namespace KioskDevice.Services.Implementations
{
    public class CallSystemService : ICallSystemService
    {
        private readonly ILogger<CallSystemService> _logger;
        private readonly string _callSystemComPort;

        public CallSystemService(ILogger<CallSystemService> logger, IConfiguration config)
        {
            _logger = logger;
            _callSystemComPort = config.GetValue<string>("Devices:CallSystemComPort", "COM4");
        }

        public async Task<bool> CallTicketAsync(CallCommand command)
        {
            try
            {
                var callData = $"CALL|{command.TicketNumber}|{command.CounterNumber}|{command.DepartmentName}";
                await SendCommandToCallSystemAsync(callData);
                await PlayAudioAsync($"ticket_{command.TicketNumber}");
                
                _logger.LogInformation($"Called ticket: {command.TicketNumber}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Call system error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ResetCallAsync()
        {
            try
            {
                return await SendCommandToCallSystemAsync("RESET");
            }
            catch { return false; }
        }

        public async Task<bool> PlayAudioAsync(string audioFile)
        {
            try
            {
                var audioPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio", $"{audioFile}.wav");
                
                if (!System.IO.File.Exists(audioPath))
                    audioPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio", "default.wav");

                using (var player = new System.Media.SoundPlayer(audioPath))
                {
                    await Task.Run(() => player.PlaySync());
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Audio play error: {ex.Message}");
                return false;
            }
        }

        public async Task<int> GetCallSystemStatusAsync()
        {
            try
            {
                var status = await SendCommandToCallSystemAsync("STATUS");
                return status ? 1 : 2; // 1=ON, 2=ERROR
            }
            catch { return 2; }
        }

        private async Task<bool> SendCommandToCallSystemAsync(string command)
        {
            try
            {
                using (var port = new SerialPort(_callSystemComPort, 9600))
                {
                    if (port.IsOpen) port.Close();
                    port.Open();
                    port.WriteLine(command);
                    port.Close();
                    return true;
                }
            }
            catch { return false; }
        }
    }
}