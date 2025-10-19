using System;
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
            _printerName = config.GetValue<string>("Devices:PrinterName", "POS Printer");
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

                var ticketContent = GenerateTicketContent(command);
                await PrintContentAsync(ticketContent);

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
                // Kiểm tra trạng thái printer qua WMI
                var query = new ObjectQuery($"SELECT * FROM Win32_Printer WHERE Name='{_printerName}'");
                var searcher = new ManagementObjectSearcher(query);
                var collection = searcher.Get();

                if (collection.Count == 0) return false;

                foreach (ManagementObject printer in collection)
                {
                    var status = (uint)printer["PrinterStatus"];
                    return status == 0; // 0 = Ready
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Printer check error: {ex.Message}");
                return false;
            }
        }

        public async Task<string> GetPrinterStatusAsync()
        {
            var statuses = new System.Collections.Generic.Dictionary<uint, string>
            {
                { 0, "Ready" },
                { 1, "Paused" },
                { 2, "Error" },
                { 3, "PaperOut" },
                { 4, "ManualFeed" }
            };

            try
            {
                var query = new ObjectQuery($"SELECT * FROM Win32_Printer WHERE Name='{_printerName}'");
                var searcher = new ManagementObjectSearcher(query);
                var collection = searcher.Get();

                foreach (ManagementObject printer in collection)
                {
                    var status = (uint)printer["PrinterStatus"];
                    return statuses.ContainsKey(status) ? statuses[status] : "Unknown";
                }
            }
            catch { }
            
            return "Disconnected";
        }

        private string GenerateTicketContent(PrintCommand command)
        {
            return $@"
╔══════════════════════════════╗
║    BỆnh VIỆN TRUNG ƯƠNG      ║
║    PHIẾU LƯỢT KHÁM BỆNH      ║
╠══════════════════════════════╣
║ Số thứ tự: {command.TicketNumber,-18} ║
║ Khoa: {command.DepartmentName,-23} ║
║ Vị trí: {command.QueuePosition,-24} ║
║ Thời gian: {command.CreatedAt:HH:mm:ss,-19} ║
╠══════════════════════════════╣
║ Vui lòng chờ đợi lượt của    ║
║ bạn. Hãy lắng nghe gọi tên   ║
║ hoặc xem bảng hiển thị       ║
╚══════════════════════════════╝
";
        }

        private async Task PrintContentAsync(string content)
        {
            // Sử dụng Windows Print Spooler API
            using (var printDocument = new System.Drawing.Printing.PrintDocument())
            {
                printDocument.PrinterSettings.PrinterName = _printerName;
                printDocument.PrintPage += (sender, e) =>
                {
                    e.Graphics.DrawString(content, new System.Drawing.Font("Courier New", 10), 
                        System.Drawing.Brushes.Black, 10, 10);
                    e.HasMorePages = false;
                };
                
                await Task.Run(() => printDocument.Print());
            }
        }
    }
}