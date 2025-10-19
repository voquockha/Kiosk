using System.Threading.Tasks;
using KioskDevice.Models;

namespace KioskDevice.Services.Interfaces
{
    public interface IBackendCommunicationService
    {
        Task<DeviceCommandResponse> GetPendingCommandsAsync();
        Task<bool> SendHeartbeatAsync(DeviceStatusDto status);
        Task<bool> ReportErrorAsync(string errorMessage, string errorType);
        Task<bool> AcknowledgeCommandAsync(string commandId);
    }
}
