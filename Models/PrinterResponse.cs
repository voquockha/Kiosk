namespace KioskDevice.Models
{
    public class PrinterResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string TicketNumber { get; set; }
    }
}