using System.Threading.Tasks;
using KioskDevice.Models;

namespace KioskDevice.Services.Interfaces
{
    public interface ICallSystemService
    {
        Task<bool> CallTicketAsync(CallCommand command);
        Task<bool> ResetCallAsync();
        Task<bool> PlayAudioAsync(string audioFile);
        Task<int> GetCallSystemStatusAsync();
    }
}
