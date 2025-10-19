using System.Threading.Tasks;
using KioskDevice.Models;

namespace KioskDevice.Services.Interfaces
{
    public interface IDeviceOrchestrator
    {
        Task ProcessPrintCommandAsync(PrintCommand command);
        Task ProcessCallCommandAsync(CallCommand command);
        Task<DeviceStatusDto> GetDeviceStatusAsync();
    }
}