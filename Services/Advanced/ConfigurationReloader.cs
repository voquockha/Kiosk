
namespace KioskDevice.Services.Advanced
{
    using KioskDevice.Models;
    using KioskDevice.Services.Interfaces;
    using System.Collections.Concurrent;
    public interface IConfigurationReloader
    {
        Task ReloadConfigAsync();
        event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
    }

    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string Setting { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
    }

    public class ConfigurationReloader : IConfigurationReloader
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurationReloader> _logger;
        public event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        public ConfigurationReloader(IConfiguration configuration, ILogger<ConfigurationReloader> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task ReloadConfigAsync()
        {
            try
            {
                if (_configuration is IConfigurationRoot root)
                {
                    root.Reload();
                    _logger.LogInformation("Configuration reloaded successfully");
                    ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs { Setting = "All" });
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Configuration reload failed: {ex.Message}");
            }
        }
    }
}