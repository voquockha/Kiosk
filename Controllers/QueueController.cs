using KioskDevice.Models;
using KioskDevice.Services.Advanced;
using KioskDevice.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace KioskDevice.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueueController : ControllerBase
    {
        private readonly ICommandQueueService _queueService;
        private readonly ILogger<QueueController> _logger;

        public QueueController(ICommandQueueService queueService, ILogger<QueueController> logger)
        {
            _queueService = queueService;
            _logger = logger;
        }

        /// <summary>
        /// Lấy số lệnh trong queue
        /// GET /api/queue/count
        /// </summary>
        [HttpGet("count")]
        public IActionResult GetQueueCount()
        {
            try
            {
                var count = _queueService.GetQueueCount();
                return Ok(new { count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Clear queue
        /// DELETE /api/queue/clear
        /// </summary>
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearQueue()
        {
            try
            {
                await _queueService.ClearQueueAsync();
                return Ok(new { success = true, message = "Queue cleared" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}