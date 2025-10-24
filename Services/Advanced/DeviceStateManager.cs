using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Reactive.Linq;

namespace KioskDevice.Services.Advanced
{
    public enum DeviceState
    {
        Initializing,
        Ready,      // Sẵn sàng
        Printing,   // Đang in (tránh spam)
        Calling,     // Đang gọi (tránh spam)
        Error,
        Maintenance
    }

    public interface IDeviceStateManager
    {
        DeviceState GetCurrentState();
        Task ChangeStateAsync(DeviceState newState, string reason);
        Task<bool> CanProcessCommandAsync(string commandType);
        IObservable<DeviceState> StateChanges { get; }
    }

    public class DeviceStateManager : IDeviceStateManager
    {
        private DeviceState _currentState = DeviceState.Ready;
        private readonly Subject<DeviceState> _stateChanges = new();
        private readonly ILogger<DeviceStateManager> _logger;

        public IObservable<DeviceState> StateChanges => _stateChanges.AsObservable();

        public DeviceStateManager(ILogger<DeviceStateManager> logger)
        {
            _logger = logger;
        }

        public DeviceState GetCurrentState() => _currentState;

        public async Task ChangeStateAsync(DeviceState newState, string reason)
        {
            if (_currentState != newState)
            {
                _logger.LogInformation($"State: {_currentState} → {newState}. Reason: {reason}");
                _currentState = newState;
                _stateChanges.OnNext(newState);
            }
            await Task.CompletedTask;
        }

        public async Task<bool> CanProcessCommandAsync(string commandType)
        {
            // Chỉ kiểm tra SPAM - không chặn vì lỗi thiết bị
            return _currentState switch
            {
                DeviceState.Ready or DeviceState.Initializing => true, // OK
                
                // Đang in → Chỉ chặn PRINT, cho phép CALL
                DeviceState.Printing => commandType != "PRINT",
                
                // Đang gọi → Chỉ chặn CALL, cho phép PRINT
                DeviceState.Calling => commandType != "CALL",
                
                _ => true
            };
        }
    }
}