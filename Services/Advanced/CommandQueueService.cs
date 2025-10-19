namespace KioskDevice.Services.Advanced
{
    using KioskDevice.Models;
    using KioskDevice.Services.Interfaces;
    using System.Collections.Concurrent;

    // ========== 1. QUEUE MANAGEMENT ==========
    public interface ICommandQueueService
    {
        Task EnqueueCommandAsync(DeviceCommandResponse command);
        Task<DeviceCommandResponse> DequeueCommandAsync();
        int GetQueueCount();
        Task ClearQueueAsync();
    }

    public class CommandQueueService : ICommandQueueService
    {
        private readonly ConcurrentQueue<DeviceCommandResponse> _queue = new();
        private readonly ILogger<CommandQueueService> _logger;
        private readonly SemaphoreSlim _queueSemaphore = new(0);

        public CommandQueueService(ILogger<CommandQueueService> logger)
        {
            _logger = logger;
        }

        public async Task EnqueueCommandAsync(DeviceCommandResponse command)
        {
            _queue.Enqueue(command);
            _queueSemaphore.Release();
            _logger.LogInformation($"Command enqueued: {command.CommandId}, Queue size: {_queue.Count}");
        }

        public async Task<DeviceCommandResponse> DequeueCommandAsync()
        {
            await _queueSemaphore.WaitAsync();
            _queue.TryDequeue(out var command);
            return command;
        }

        public int GetQueueCount() => _queue.Count;

        public async Task ClearQueueAsync()
        {
            while (_queue.TryDequeue(out _)) { }
            _logger.LogWarning("Command queue cleared");
        }
    }
}