
namespace KioskDevice.Services.Advanced
{
    public interface IRetryPolicy
    {
        Task<T> ExecuteAsync<T>(Func<Task<T>> action, string operationName);
    }

    public class ExponentialBackoffRetryPolicy : IRetryPolicy
    {
        private readonly ILogger<ExponentialBackoffRetryPolicy> _logger;
        private readonly int _maxRetries = 3;
        private readonly int _initialDelayMs = 1000;

        public ExponentialBackoffRetryPolicy(ILogger<ExponentialBackoffRetryPolicy> logger)
        {
            _logger = logger;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, string operationName)
        {
            int attempt = 0;
            while (attempt < _maxRetries)
            {
                try
                {
                    _logger.LogInformation($"Executing {operationName}, attempt {attempt + 1}/{_maxRetries}");
                    return await action();
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt >= _maxRetries)
                    {
                        _logger.LogError($"Operation {operationName} failed after {_maxRetries} attempts: {ex.Message}");
                        throw;
                    }

                    var delayMs = _initialDelayMs * (int)Math.Pow(2, attempt - 1);
                    _logger.LogWarning($"Operation {operationName} failed, retrying in {delayMs}ms: {ex.Message}");
                    await Task.Delay(delayMs);
                }
            }
            throw new InvalidOperationException($"Operation {operationName} failed");
        }
    }
}