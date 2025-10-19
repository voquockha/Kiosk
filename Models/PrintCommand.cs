using System;
namespace KioskDevice.Models
{
    public class PrintCommand
    {
        public string TicketNumber { get; set; }
        public string DepartmentName { get; set; }
        public int QueuePosition { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}