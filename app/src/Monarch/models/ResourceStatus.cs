namespace Monarch.Models
{
    public enum StatusType
    {
        Unknown,
        Operational,
        DegradedPerformance,
        Outage
    }

    public class ResourceStatus
    {
        public string ResourceName { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
    }

    public class ApplicationStatus : ResourceStatus
    {
        public string Id { get; set; } = string.Empty;
        public string Name
        {
            get => ResourceName;
            set => ResourceName = value;
        }
        public StatusType Status { get; set; } = StatusType.Unknown;
    }
}
