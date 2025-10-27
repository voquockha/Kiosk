using System;

namespace KioskDevice.Models
{
    public class CallCommand
    {
        public string TicketNumber { get; set; }
        public string DepartmentName { get; set; }
        public string CounterNumber { get; set; }
        public string Status { get; set; }
        public string AudioPath { get; set; }
    }
}