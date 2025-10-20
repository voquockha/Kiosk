using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KioskDevice.Models;
using KioskDevice.Services.Interfaces;

namespace KioskDevice.Services.Implementations
{
    public class DeviceOrchestrator : IDeviceOrchestrator
    {
        private readonly ILogger<DeviceOrchestrator> _logger;
        private readonly IPrinterService _printerService;
        private readonly IDisplayService _displayService;
        private readonly ICallSystemService _callSystemService;
        private readonly IBackendCommunicationService _backendService;

        public DeviceOrchestrator(
            ILogger<DeviceOrchestrator> logger,
            IPrinterService printerService,
            IDisplayService displayService,
            ICallSystemService callSystemService,
            IBackendCommunicationService backendService)
        {
            _logger = logger;
            _printerService = printerService;
            _displayService = displayService;
            _callSystemService = callSystemService;
            _backendService = backendService;
        }

        public async Task ProcessPrintCommandAsync(PrintCommand command)
        {
            try
            {
                _logger.LogInformation($"Processing print command for ticket: {command.TicketNumber}");

                // Hiển thị trên màn hình trước
                await _displayService.DisplayTicketAsync(
                    command.TicketNumber,
                    command.DepartmentName,
                    command.QueuePosition
                );

                // In phiếu
                var printResult = await _printerService.PrintTicketAsync(command);

                if (!printResult.Success)
                {
                    throw new Exception($"Print failed: {printResult.Message}");
                }

                _logger.LogInformation($"Print completed for ticket {command.TicketNumber}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Print command processing error: {ex.Message}");
                throw;
            }
        }

        public async Task ProcessCallCommandAsync(CallCommand command)
        {
            try
            {
                _logger.LogInformation($"Processing call command for ticket: {command.TicketNumber}");

                // Gọi số
                await _callSystemService.CallTicketAsync(command);

                // Hiển thị trên màn hình
                await _displayService.DisplayMessageAsync(
                    $"Khám tại quầy {command.CounterNumber}"
                );

                _logger.LogInformation($"Called ticket {command.TicketNumber} to counter {command.CounterNumber}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Call command processing error: {ex.Message}");
                throw;
            }
        }

        public async Task<DeviceStatusDto> GetDeviceStatusAsync()
        {
            try
            {
                var printerReady = await _printerService.IsPrinterReadyAsync();
                var callStatus = await _callSystemService.GetCallSystemStatusAsync();

                return new DeviceStatusDto
                {
                    DeviceId = "KIOSK-001",
                    Status = printerReady ? "ONLINE" : "ERROR",
                    PrinterStatus = await _printerService.GetPrinterStatusAsync(),
                    DisplayStatus = "OK",
                    CallSystemStatus = callStatus,
                    LastHeartbeat = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Get device status error: {ex.Message}");
                throw;
            }
        }
    }
}