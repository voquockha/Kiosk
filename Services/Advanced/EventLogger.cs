namespace KioskDevice.Services.Advanced
{
    using KioskDevice.Models;
    using KioskDevice.Services.Interfaces;
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging;
    using System.Threading;

    public enum EventType
    {
        PrintStarted,
        PrintCompleted,
        PrintFailed,
        CallStarted,
        CallMissed,
        CallCompleted,
        DeviceError,
        DeviceOnline,
        DeviceOffline,
        HeartbeatSent,
        CommandReceived
    }

    public class DeviceEvent
    {
        public string EventId { get; set; }
        public EventType Type { get; set; }
        public string TicketNumber { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    public interface IEventLogger
    {
        Task LogEventAsync(DeviceEvent evt);
        Task<List<DeviceEvent>> GetRecentEventsAsync(int count = 100);
        Task ExportEventsAsync(string filePath);
    }

    public class EventLogger : IEventLogger
    {
        private readonly ConcurrentQueue<DeviceEvent> _events = new();
        private readonly ILogger<EventLogger> _logger;
        private readonly string _logDirectory;

        public EventLogger(ILogger<EventLogger> logger)
        {
            _logger = logger;

            _logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs", "Events");

            // Tạo thư mục nếu chưa có
            Directory.CreateDirectory(_logDirectory);
        }

        public async Task LogEventAsync(DeviceEvent evt)
        {
            evt.EventId = Guid.NewGuid().ToString();
            evt.Timestamp = DateTime.UtcNow;
            _events.Enqueue(evt);

            var logEntry = $"[{evt.Timestamp:yyyy-MM-dd HH:mm:ss}] {evt.Type}: {evt.Description}";
            _logger.LogInformation(logEntry);

            await Task.Run(() =>
            {
                var logFile = Path.Combine(_logDirectory, $"events-{DateTime.UtcNow:yyyy-MM-dd}.log");
                File.AppendAllText(logFile, logEntry + Environment.NewLine);
            });
        }

        public async Task<List<DeviceEvent>> GetRecentEventsAsync(int count = 100)
        {
            return await Task.FromResult(_events.TakeLast(count).ToList());
        }

        public async Task ExportEventsAsync(string filePath)
        {
            var events = _events.ToList();
            var csv = "EventId,Type,TicketNumber,Description,Timestamp\n";

            foreach (var evt in events)
            {
                csv += $"{evt.EventId},{evt.Type},{evt.TicketNumber},{evt.Description},{evt.Timestamp:O}\n";
            }

            await File.WriteAllTextAsync(filePath, csv);
            _logger.LogInformation($"Events exported to {filePath}");
        }
    }
}