using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using KioskDevice.Services;
using KioskDevice.Services.Advanced;
using KioskDevice.Services.Implementations;
using KioskDevice.Services.Interfaces;
using KioskDevice.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ========== ĐĂNG KÝ SERVICES ==========

// HttpClient cho Backend
builder.Services.AddHttpClient("BackendApi", client =>
{
    var baseUrl = builder.Configuration["Backend:BaseUrl"] ?? "http://localhost:5000";
    client.BaseAddress = new Uri(baseUrl);
    var timeout = builder.Configuration.GetValue<int>("Backend:Timeout", 30);
    client.Timeout = TimeSpan.FromSeconds(timeout);
});

// Core Services
builder.Services.AddSingleton<IPrinterService, PrinterService>();
builder.Services.AddSingleton<IDisplayService, DisplayService>();
builder.Services.AddSingleton<ICallSystemService, CallSystemService>();
builder.Services.AddSingleton<IBackendCommunicationService, MockBackendCommunicationService>();
builder.Services.AddSingleton<IDeviceOrchestrator, DeviceOrchestrator>();

// Advanced Services
builder.Services.AddSingleton<ICommandQueueService, CommandQueueService>();
builder.Services.AddSingleton<IRetryPolicy, ExponentialBackoffRetryPolicy>();
builder.Services.AddSingleton<IDeviceStateManager, DeviceStateManager>();
builder.Services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
builder.Services.AddSingleton<IEventLogger, EventLogger>();
builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();
builder.Services.AddSingleton<IConfigurationReloader, ConfigurationReloader>();
builder.Services.AddHostedService<DisplayInitializer>();

// Controllers và CORS
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin()
                     .AllowAnyMethod()
                     .AllowAnyHeader();
    });
});

// ========== BUILD APP ==========
var app = builder.Build();

// ========== CONFIGURE URL ==========
app.Urls.Clear();
app.Urls.Add("http://0.0.0.0:5001");

Console.WriteLine("=== Cấu hình Services ===");

// ========== ADD MIDDLEWARE ==========
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<PerformanceMiddleware>();

app.UseRouting();
app.UseCors("AllowAll");

// ========== MAP CONTROLLERS ==========
app.MapControllers();

Console.WriteLine("=== KioskDevice start http://0.0.0.0:5001 ===");

// ========== RUN APP ==========
await app.RunAsync();