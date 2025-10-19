using System;

namespace KioskDevice.Models
{
    public class DeviceStatusDto
    {
        public string DeviceId { get; set; }
        public string Status { get; set; } // ONLINE, OFFLINE, ERROR
        public string PrinterStatus { get; set; }
        public string DisplayStatus { get; set; }
        public int CallSystemStatus { get; set; } // 0=OFF, 1=ON, 2=ERROR
        public DateTime LastHeartbeat { get; set; }
    }
}
