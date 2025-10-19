using KioskDevice.Services.Advanced;
namespace KioskDevice.Middleware
{

    public class PerformanceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IPerformanceMonitor _performanceMonitor;
        private readonly ILogger<PerformanceMiddleware> _logger;

        public PerformanceMiddleware(RequestDelegate next, IPerformanceMonitor performanceMonitor, ILogger<PerformanceMiddleware> logger)
        {
            _next = next;
            _performanceMonitor = performanceMonitor;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var startTime = DateTime.UtcNow;
            await _next(context);
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            var operationName = $"{context.Request.Method} {context.Request.Path}";
            _performanceMonitor.RecordOperation(operationName, (long)duration);

            if (duration > 5000)
            {
                _logger.LogWarning($"Slow operation: {operationName} took {duration}ms");
            }
        }
    }
}
