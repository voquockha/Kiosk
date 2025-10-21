using System;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Media;
using System.Collections.Generic;
using KioskDevice.Models;
using KioskDevice.Services.Interfaces;
using NAudio.Wave;


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

        /// <summary>
        /// Gọi số thứ tự và phát âm thanh
        /// </summary>
        public async Task<bool> CallTicketAsync(CallCommand command)
        {
            try
            {
                // Gửi lệnh đến thiết bị gọi số qua COM port
                var callData = $"CALL|{command.TicketNumber}|{command.CounterNumber}|{command.DepartmentName}";
                // await SendCommandToCallSystemAsync(callData);

                // Phát âm thanh
                await PlayCallSentenceAsync(command);

                _logger.LogInformation($"✅ Called ticket: {command.TicketNumber}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Call system error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Phát câu "Xin mời quý khách số thứ tự X vào quầy Y số Z"
        /// bằng cách phát nhiều file .wav liên tiếp
        /// </summary>
        private async Task<bool> PlayCallSentenceAsync(CallCommand command)
        {
            try
            {
                string basePath = @"D:\WorkSpaces\Kiosk\Audio";

                var audioFiles = new List<string>
                {
                    "Vaoquaytiepnhanso.mp3",
                    $"{command.CounterNumber}.mp3",
                    $"{command.TicketNumber}.mp3",
                };

                foreach (var fileName in audioFiles)
                {
                    var path = Path.Combine(basePath, fileName);
                    if (!File.Exists(path))
                    {
                        _logger.LogWarning($"⚠️ File không tồn tại: {path}, bỏ qua...");
                        continue;
                    }

                    await Task.Run(() =>
                    {
                        using (var audioFile = new AudioFileReader(path))
                        using (var outputDevice = new WaveOutEvent())
                        {
                            outputDevice.Init(audioFile);
                            outputDevice.Play();

                            while (outputDevice.PlaybackState == PlaybackState.Playing)
                            {
                                Task.Delay(100).Wait();
                            }
                        }
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Audio play error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Chuẩn hoá tên file từ tên quầy — ví dụ: "Quầy đất đai" => "dat_dai"
        /// </summary>
        private string NormalizeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "default";

            var result = input.ToLower()
                              .Replace("quầy", "")
                              .Replace(" ", "_")
                              .Replace("đ", "d");
            return result.Trim('_');
        }

        public async Task<bool> ResetCallAsync()
        {
            try
            {
                return await SendCommandToCallSystemAsync("RESET");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Phát 1 file âm thanh riêng lẻ (hàm cũ vẫn giữ lại cho mục đích khác)
        /// </summary>
        public async Task<bool> PlayAudioAsync(string audioFile)
        {
            try
            {
                var basePath = @"D:\WorkSpaces\Kiosk\Audio";
                var audioPath = Path.Combine(basePath, $"{audioFile}.wav");

                if (!File.Exists(audioPath))
                    audioPath = Path.Combine(basePath, "default.wav");

                using (var player = new SoundPlayer(audioPath))
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
            catch
            {
                return 2;
            }
        }

        private async Task<bool> SendCommandToCallSystemAsync(string command)
        {
            try
            {
                _logger.LogDebug("Todo led");
                return true;
                // using (var port = new SerialPort(_callSystemComPort, 9600))
                // {
                //     if (port.IsOpen) port.Close();
                //     port.Open();
                //     port.WriteLine(command);
                //     port.Close();
                //     return true;
                // }
            }
            catch
            {
                return false;
            }
        }
    }
}
