using Pushthrough.Web.Models;
using dotAPNS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pushthrough.Web.Services.Interface
{
    public interface IPushNotificationService<TResult>
    {
        Task<PushServiceResult< ApnsResponse>> SendAsync(byte[] privateKey, NotificationModel model, CancellationToken ct = default);
    }
}
