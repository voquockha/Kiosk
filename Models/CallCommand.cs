namespace KioskDevice.Models
{
    public class CallCommand
    {
        public string TicketNumber { get; set; }
        public string DepartmentName { get; set; }
        public int CounterNumber { get; set; }
        public string Status { get; set; } // "CALLING", "MISSED", "COMPLETED"
    }
}