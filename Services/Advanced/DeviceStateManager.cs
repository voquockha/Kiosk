using System.Reactive.Subjects;
using System.Reactive.Linq;
namespace KioskDevice.Services.Advanced
{
    public enum DeviceState
    {
        Initializing,
        Ready,
        Printing,
        Calling,
        Error,
        Maintenance,
        Offline
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
        private DeviceState _currentState = DeviceState.Initializing;
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
                _logger.LogInformation($"State changed from {_currentState} to {newState}. Reason: {reason}");
                _currentState = newState;
                _stateChanges.OnNext(newState);
                await Task.CompletedTask;
            }
        }

        public async Task<bool> CanProcessCommandAsync(string commandType)
        {
            return _currentState switch
            {
                DeviceState.Ready => true,
                DeviceState.Offline => false,
                DeviceState.Error => commandType == "RESET",
                DeviceState.Maintenance => false,
                _ => false
            };
        }
    }
}