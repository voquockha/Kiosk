using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using KioskDevice.Services.Implementations;
using KioskDevice.Models;

namespace KioskDevice.Tests
{
    public class PrinterServiceTests
    {
        private readonly Mock<ILogger<PrinterService>> _loggerMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly PrinterService _printerService;

        public PrinterServiceTests()
        {
            _loggerMock = new Mock<ILogger<PrinterService>>();
            _configMock = new Mock<IConfiguration>();
            _configMock
                .Setup(x => x.GetValue<string>("Devices:PrinterName", It.IsAny<string>()))
                .Returns("POS Printer");

            _printerService = new PrinterService(_loggerMock.Object, _configMock.Object);
        }

        [Fact]
        public async Task PrintTicketAsync_WithValidCommand_ShouldReturnSuccess()
        {
            // Arrange
            var command = new PrintCommand
            {
                TicketNumber = "A001",
                DepartmentName = "Khám Tổng Quát",
                QueuePosition = 1,
                CreatedAt = System.DateTime.UtcNow
            };

            // Act
            var result = await _printerService.PrintTicketAsync(command);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("A001", result.TicketNumber);
        }
    }
}
