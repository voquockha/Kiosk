using System.Threading.Tasks;

namespace KioskDevice.Services.Interfaces
{
    public interface IDisplayService
    {
        Task<bool> DisplayTicketAsync(string ticketNumber, string department, int position);
        Task<bool> ClearDisplayAsync();
        Task<bool> DisplayMessageAsync(string message);
    }
}
