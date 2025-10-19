using System;

namespace KioskDevice.Models
{
    public class DeviceCommandResponse
    {
        public string CommandId { get; set; }
        public string Type { get; set; } // "PRINT", "CALL", "DISPLAY"
        public object Data { get; set; }
        public DateTime Timestamp { get; set; }
    }
}