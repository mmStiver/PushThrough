using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pushthrough.Web.Models
{
    public record ApnsPushRequestModel
    {
        public byte[] PrivateKey { get; init; }
        public List<NotificationModel> Notifications { get; init; } = new List<NotificationModel>();
    }
}
