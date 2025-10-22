using System;
using System.Collections.Generic;

namespace KioskDevice.Models
{
    // Response từ Backend
    public class ApiResponse<T>
    {
        public string CommandId { get; set; }
        public string DeviceId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
        public bool Status { get; set; }
        public T Data { get; set; }
    }

    // Command data từ Backend
    public class CommandData
    {
        public string TicketNumber { get; set; }
        public string DepartmentName { get; set; }
        public int QueuePosition { get; set; }
        public string CounterNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Path { get; set; }
    }

    // Heartbeat request
    public class HeartbeatRequest
    {
        public string CommandId { get; set; }
        public string DeviceId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Version { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public HeartbeatData Data { get; set; }
    }

    public class HeartbeatData
    {
        public DeviceInfo Speaker { get; set; }
        public DeviceInfo Printer { get; set; }
    }

    public class DeviceInfo
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public int? Volume { get; set; }
        public string Paper { get; set; }
        public string Temp { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsPrinting { get; set; }
    }

    // Command result request
    public class CommandResultRequest
    {
        public string CommandId { get; set; }
        public string DeviceId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Version { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public object Data { get; set; }
    }

    // Error report request
    public class ErrorReportRequest
    {
        public string CommandId { get; set; }
        public string DeviceId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Version { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public object Data { get; set; }
    }

    // Print response
    public class PrintResponse
    {
        public string CommandId { get; set; }
        public string DeviceId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
        public bool Status { get; set; }
        public PrintResponseData Data { get; set; }
    }

    public class PrintResponseData
    {
        public string TicketNumber { get; set; }
    }

    // Call response
    public class CallResponse
    {
        public string CommandId { get; set; }
        public string DeviceId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
        public bool Status { get; set; }
        public CallResponseData Data { get; set; }
    }

    public class CallResponseData
    {
        public string TicketNumber { get; set; }
    }
}
