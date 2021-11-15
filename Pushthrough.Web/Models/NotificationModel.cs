using System.Collections.Generic;

namespace Pushthrough.Web.Models
{
    public record NotificationModel
    {
        public long NotificationId { get; init; }
        public string DeviceToken { get; init; }
        public string Title { get; init; }
        public string Subtitle { get; init; }
        public string Text { get; init; }
        public string Category { get; init; }
        public int Badge { get; init; }
        public string Sound { get; init; }
        public bool ContentAvailable { get; init; }
        public Dictionary<string, string> CustomProperties { get; init; }
    }
}