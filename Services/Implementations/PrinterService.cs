using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using KioskDevice.Models;
using KioskDevice.Services.Interfaces;

namespace KioskDevice.Services.Implementations
{
    public class PrinterService : IPrinterService
    {
        private readonly ILogger<PrinterService> _logger;
        private readonly string _printerName;

        public PrinterService(ILogger<PrinterService> logger, IConfiguration config)
        {
            _logger = logger;
            _printerName = config.GetValue<string>("Devices:PrinterName", "EPSON TM-T81III Receipt");
        }

        public async Task<PrinterResponse> PrintTicketAsync(PrintCommand command)
        {
            try
            {
                var isReady = await IsPrinterReadyAsync();
                if (!isReady)
                {
                    return new PrinterResponse
                    {
                        Success = false,
                        Message = "Printer not ready"
                    };
                }

                // 👉 Thay vì in text, bạn in ảnh
                // Ví dụ: đường dẫn ảnh hóa đơn
                string imagePath = command.FilePath ?? @"D:\Khavq\app\PRINT_IN_7-1.jpg";

                await PrintImageAsync(imagePath);

                _logger.LogInformation($"Ticket {command.TicketNumber} printed successfully");

                return new PrinterResponse
                {
                    Success = true,
                    Message = "Printed successfully",
                    TicketNumber = command.TicketNumber
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Print error: {ex.Message}");
                return new PrinterResponse
                {
                    Success = false,
                    Message = $"Print failed: {ex.Message}"
                };
            }
        }

        public async Task<bool> IsPrinterReadyAsync()
        {
            try
            {
                using var query = new ManagementObjectSearcher("SELECT * FROM Win32_Printer");
                foreach (ManagementObject printer in query.Get())
                {
                    var printerName = printer["Name"]?.ToString();

                    if (printerName == _printerName)
                    {
                        var statusObj = printer["PrinterStatus"];
                        if (statusObj == null)
                        {
                            _logger.LogWarning("PrinterStatus is null");
                            return true;
                        }

                        try
                        {
                            var status = Convert.ToUInt32(statusObj);
                            _logger.LogInformation($"Printer status code: {status}");
                            return status == 0 || status == 3; // 0=Ready, 3=Idle
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Cannot convert status: {ex.Message}");
                            return true;
                        }
                    }
                }

                _logger.LogWarning($"Printer not found: {_printerName}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Printer check error: {ex.Message}");
                return false;
            }
        }

        public async Task<string> GetPrinterStatusAsync()
        {
            var statuses = new Dictionary<uint, string>
            {
                { 0, "Ready" },
                { 1, "Paused" },
                { 2, "Error" },
                { 3, "Ready" },
                { 4, "ManualFeed" }
            };

            try
            {
                using var query = new ManagementObjectSearcher("SELECT * FROM Win32_Printer");
                foreach (ManagementObject printer in query.Get())
                {
                    if (printer["Name"]?.ToString() == _printerName)
                    {
                        var statusObj = printer["PrinterStatus"];
                        if (statusObj != null)
                        {
                            try
                            {
                                var status = Convert.ToUInt32(statusObj);
                                return statuses.ContainsKey(status) ? statuses[status] : $"Unknown({status})";
                            }
                            catch
                            {
                                return "Unknown";
                            }
                        }
                        return "Ready";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Get printer status error: {ex.Message}");
            }

            return "Disconnected";
        }

        private async Task PrintImageAsync(string imagePath)
        {
            using var printDocument = new PrintDocument();
            printDocument.PrinterSettings.PrinterName = _printerName;

            printDocument.PrintPage += (sender, e) =>
            {
                using (var img = Image.FromFile(imagePath))
                {
                    float targetWidthMm = 40f;
                    float dpi = e.Graphics.DpiX; // mật độ điểm ảnh thực tế
                    float targetWidthPx = targetWidthMm / 25.4f * dpi; // đổi mm → px

                    // Tính tỉ lệ scale để giữ nguyên tỷ lệ ảnh
                    float scale = targetWidthPx / img.Width;
                    float targetHeightPx = img.Height * scale;

                    // Căn giữa ảnh theo chiều ngang
                    float startX = (e.PageBounds.Width - targetWidthPx) / 2 - 20;
                    float startY = 0; // bạn có thể thêm margin nếu muốn

                    e.Graphics.DrawImage(img, startX, startY, targetWidthPx, targetHeightPx);
                }
                e.HasMorePages = false;
            };

            await Task.Run(() => printDocument.Print());
        }
    }
}
