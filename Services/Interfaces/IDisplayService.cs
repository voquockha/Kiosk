using System.Threading.Tasks;

namespace KioskDevice.Services.Interfaces
{
    public interface IDisplayService
    {
        Task<bool> DisplayMessageAsync(string ticketNumber, string counterNumber);
    }
}
