namespace KioskDevice.Models
{
    public class MaintenanceRequest
    {
        public string Reason { get; set; }
        public int? EstimatedDurationMinutes { get; set; }
    }

    public class ErrorReport
    {
        public string ErrorType { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Details { get; set; }
    }

    public class CommandAcknowledgement
    {
        public string CommandId { get; set; }
        public string Status { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}