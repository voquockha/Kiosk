using System.Threading.Tasks;
using KioskDevice.Models;

namespace KioskDevice.Services.Interfaces
{
    public interface IPrinterService
    {
        Task<PrinterResponse> PrintTicketAsync(PrintCommand command);
        Task<bool> IsPrinterReadyAsync();
        Task<string> GetPrinterStatusAsync();
    }
}