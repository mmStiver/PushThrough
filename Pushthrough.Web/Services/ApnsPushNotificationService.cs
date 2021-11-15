using Pushthrough.Web.Models;
using Pushthrough.Web.Services.Interface;
using dotAPNS;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;

namespace Pushthrough.Web.Services
{
    public class ApnsPushNotificationService : IPushNotificationService<PushServiceResult<ApnsResponse>>
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<ApnsPushNotificationService> _logger;
        private readonly string _certificatePwd;
        private readonly int _expirateSeconds;
        private readonly bool _useSandbox;
        private readonly IApnsClient _client;

        [ActivatorUtilitiesConstructor]
        public ApnsPushNotificationService(IMemoryCache cache, IConfiguration config, ILogger<ApnsPushNotificationService> logger)
        {
            _cache = cache;
            _certificatePwd = config["APNSettings:APNSPassword"];
            
            if(!Int32.TryParse(config["APNSettings:APNSExpiration"], out int expirationSeconds))
                _expirateSeconds =expirationSeconds;
             else 
                _expirateSeconds = 0;
            _logger = logger;
            if(Boolean.TryParse(config["APNSettings:UseAPNSSandbox"], out bool useSandbox))
                _useSandbox = useSandbox;
        }
        public ApnsPushNotificationService(IApnsClient client, ILogger<ApnsPushNotificationService> logger)
        {
            _client = client;
            _logger = logger;
        }
        public ApnsPushNotificationService(IMemoryCache cache, ILogger<ApnsPushNotificationService> logger, string password, int expiration, bool sandbox)
        {
            _cache = cache;
            _logger = logger;
            _certificatePwd = password;
            _expirateSeconds =expiration;
            _useSandbox = sandbox;
        }
       
        
        public async Task<PushServiceResult<ApnsResponse>> SendAsync(byte[] privateKey, NotificationModel model, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return GetCancellationResult(model.NotificationId, model.DeviceToken);
            
            if (String.IsNullOrEmpty(model.Text))
                return new PushServiceResult<ApnsResponse>(model.NotificationId,  model.DeviceToken, ApnsResponse.Error(ApnsResponseReason.PayloadEmpty, "PayloadEmpty"));

            if (String.IsNullOrEmpty(model.DeviceToken))
                return new PushServiceResult<ApnsResponse>(model.NotificationId, model.DeviceToken, ApnsResponse.Error(ApnsResponseReason.MissingDeviceToken, "MissingDeviceToken"));
               
            PushServiceResult<ApnsResponse> result;
            try { 
                IApnsClient apnsClient;
                if(_client != null) 
                    apnsClient = this._client;
                else 
                    apnsClient = CreateClient(privateKey);

                var alert = new ApplePush(ApplePushType.Alert)
                    .AddToken(model.DeviceToken)
                    .AddBadge(model.Badge)
                    .AddSound(model.Sound);

                if(!string.IsNullOrEmpty(model.Subtitle) && !string.IsNullOrEmpty(model.Title)) {
                    alert.AddAlert(model.Title, model.Subtitle, model.Text);
                } else if(!string.IsNullOrEmpty(model.Title)) {
                    alert.AddAlert(model.Title, model.Text);
                } else {
                    alert.AddAlert(model.Text);
                }

                if(!string.IsNullOrEmpty(model.Category))
                    alert.AddCategory(model.Category);

                if(_expirateSeconds <= 0)
                    alert.AddExpiration(DateTimeOffset.UtcNow.Add(new TimeSpan(0,0, _expirateSeconds)));
                
                if (model.ContentAvailable)
                    alert = alert.AddContentAvailable();    

                if (model.CustomProperties != null)
                {

                    foreach (var d in model.CustomProperties)
                    {
                        if (Int32.TryParse(d.Value, out int val))
                            alert.AddCustomProperty(d.Key, val);
                        else
                            alert.AddCustomProperty(d.Key, d.Value);
                    }
                    _logger.LogDebug("CustomProperties Keys: {0}", String.Join(" , ", alert.CustomProperties.Keys.Select(k => k.ToString())));
                    _logger.LogDebug("CustomProperties Values: {0}", String.Join(" , ", alert.CustomProperties.Values.Select(k => k.ToString())));
                }

                _logger.LogDebug("Sending alert: {0}", alert.Alert.Title);
                _logger.LogInformation("Sending Push notification for device {0}", model.DeviceToken);
                try
                {
                    var pushResult = await apnsClient.SendAsync(alert, ct);
                    result = new PushServiceResult<ApnsResponse>(model.NotificationId, model.DeviceToken, pushResult);
                    _logger.LogDebug("Alert response {0}: {1} - {2}", ((pushResult.IsSuccessful) ? "success" : "fail"), pushResult.Reason, pushResult.ReasonString);

                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogInformation(ex, "Push notification cancelled during send request for device {0}", model.DeviceToken);
                    result = GetCancellationResult(model.NotificationId, model.DeviceToken);
                }
            }catch (CryptographicException ex) {
                _logger.LogError("exception ", ex);
                throw ;
            }catch (Exception ex) {
                _logger.LogError("exception ", ex);
                result = new PushServiceResult<ApnsResponse>(model.NotificationId, model.DeviceToken, ApnsResponse.Error(ApnsResponseReason.InternalServerError, "InternalServerError"));
            }
            return result;
        }

        private PushServiceResult<ApnsResponse> GetCancellationResult(long id, string deviceToken)
        {
            _logger.LogInformation("Push notification cancelled before send for device {0}", deviceToken);

            var cancelledResult = ApnsResponse.Error(ApnsResponseReason.ServiceUnavailable, "Cancelled");
            return new PushServiceResult<ApnsResponse>(id, deviceToken, cancelledResult).Cancel();
        }

        private IApnsClient CreateClient(byte[] privateKey)
        {
            var certificate = new X509Certificate2(privateKey, _certificatePwd,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                _logger.LogDebug("Loading certificate : {0} \n {1}", certificate.FriendlyName, certificate.Subject);

            if (!_cache.TryGetValue(certificate.Thumbprint, out IApnsClient apns))
            {
                apns = ApnsClient.CreateUsingCert(certificate);
                _cache.Set(certificate.Thumbprint, apns, TimeSpan.FromMinutes(10));
            }
            if(_useSandbox && apns is ApnsClient)
                apns = (apns as ApnsClient).UseSandbox();
            return apns;
        }
    }
}