using System;

namespace KioskDevice.Models
{
    public class PrintCommand
    {
        public string TicketNumber { get; set; }
        public string DepartmentName { get; set; }
        public string CounterNumber { get; set; }
        public string FilePath { get; set; }
    }
}
