using System.Threading.Tasks;
using KioskDevice.Models;

namespace KioskDevice.Services.Interfaces
{
    public interface IBackendCommunicationService
    {
        Task<ApiResponse<CommandData>> GetPendingCommandsAsync();
        Task<ApiResponse<object>> SendHeartbeatAsync(HeartbeatData heartbeatData);
        Task<ApiResponse<object>> SendCommandResultAsync(string commandId, bool success, string message = "");
        Task<ApiResponse<object>> ReportErrorAsync(string commandId, string errorType, string message);
    }
}