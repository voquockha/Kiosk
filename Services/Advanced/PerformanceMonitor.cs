namespace KioskDevice.Services.Advanced
{
    using KioskDevice.Models;
    using KioskDevice.Services.Interfaces;
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging;
    using System.Threading;
    public interface IPerformanceMonitor
    {
        void RecordOperation(string operationName, long durationMs);
        Task<PerformanceStats> GetStatsAsync();
    }

    public class PerformanceStats
    {
        public string OperationName { get; set; }
        public long TotalExecutions { get; set; }
        public double AverageDurationMs { get; set; }
        public long MinDurationMs { get; set; }
        public long MaxDurationMs { get; set; }
        public int ErrorCount { get; set; }
    }

    public class PerformanceMonitor : IPerformanceMonitor
    {
        private readonly ConcurrentDictionary<string, List<long>> _operationTimings = new();
        private readonly ConcurrentDictionary<string, int> _errorCounts = new();
        private readonly ILogger<PerformanceMonitor> _logger;

        public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
        {
            _logger = logger;
        }

        public void RecordOperation(string operationName, long durationMs)
        {
            _operationTimings.AddOrUpdate(operationName,
                new List<long> { durationMs },
                (key, list) =>
                {
                    list.Add(durationMs);
                    return list;
                });

            if (durationMs > 5000)
            {
                _logger.LogWarning($"Slow operation detected: {operationName} took {durationMs}ms");
            }
        }

        public async Task<PerformanceStats> GetStatsAsync()
        {
            var allTimings = new List<long>();
            foreach (var timings in _operationTimings.Values)
            {
                allTimings.AddRange(timings);
            }

            if (allTimings.Count == 0)
                return null;

            return new PerformanceStats
            {
                TotalExecutions = allTimings.Count,
                AverageDurationMs = allTimings.Average(),
                MinDurationMs = allTimings.Min(),
                MaxDurationMs = allTimings.Max()
            };
        }
    }
}