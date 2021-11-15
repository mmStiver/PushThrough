using Pushthrough.Web.Models;
using Pushthrough.Web.Services;
using Pushthrough.Web.Services.Interface;
using dotAPNS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace Pushthrough.Web.Controllers
{
    [ApiController]
    [Route("/api/Apns")]
    public class ApnsPassthroughController : ControllerBase, IApnsPassthroughController
    {
        private readonly ILogger<ApnsPassthroughController> _logger;
        private readonly IPushNotificationService<PushServiceResult<ApnsResponse>> _apnService;
        private readonly Channel<PushServiceResult<ApnsResponse>> _responseChannel;
        private readonly SemaphoreSlim  _requestSemaphore;

        public ApnsPassthroughController(ILogger<ApnsPassthroughController> logger
            , IPushNotificationService<PushServiceResult<ApnsResponse>> apnService)
        {
            _logger = logger;
            _apnService = apnService;
            _responseChannel = Channel.CreateUnbounded<PushServiceResult<ApnsResponse>>();
            _requestSemaphore = new SemaphoreSlim(1);
        }

        [ActivatorUtilitiesConstructor]
        public ApnsPassthroughController(ILogger<ApnsPassthroughController> logger
            , IPushNotificationService<PushServiceResult<ApnsResponse>> apnService,
            IConfiguration configuration)
        {
            _logger = logger;
            _apnService = apnService;

            if(!Int32.TryParse(configuration["APNSettings:ResponseQueueSize"], out int responseSize) || responseSize == 0)
                responseSize = 100;

            _responseChannel = Channel.CreateBounded<PushServiceResult<ApnsResponse>>(
                                    new BoundedChannelOptions(responseSize){ 
                                        AllowSynchronousContinuations=true,
                                        FullMode=BoundedChannelFullMode.Wait
                                        , SingleReader=true
                                        , SingleWriter=false 
                                    });
            
            if(!Int32.TryParse(configuration["APNSettings:MaxConcurrentRequests"], out int maxConcurrentRequests) || maxConcurrentRequests == 0)
                maxConcurrentRequests = 10;
            
            _requestSemaphore = new SemaphoreSlim(maxConcurrentRequests);
        }
        
        public ApnsPassthroughController(ILogger<ApnsPassthroughController> logger
            , IPushNotificationService<PushServiceResult<ApnsResponse>> apnService, int responseQueueSize, int maxConcurrentRequests)
        {
            _logger = logger;
            _apnService = apnService;
            _responseChannel = Channel.CreateBounded<PushServiceResult<ApnsResponse>>(
                                    new BoundedChannelOptions(responseQueueSize){ 
                                        AllowSynchronousContinuations=true,
                                        FullMode=BoundedChannelFullMode.Wait, 
                                        SingleReader=true,
                                        SingleWriter=false });
            _requestSemaphore = new SemaphoreSlim(maxConcurrentRequests);
        }

        [HttpPost]
        [Route("")]
        public async Task<IActionResult> PushAsync([FromBody] ApnsPushRequestModel model, CancellationToken ct = default)
        {
            try
            {
                if (model.Notifications.Count <= 0)
                    return BadRequest("Must have at least one notification to send");
                if (model.PrivateKey == null || model.PrivateKey.Length == 0)
                    return BadRequest("Certificate not included");

                _logger.LogInformation("{0} Push requests received by api passthrough", model.Notifications.Count);
                
                var tasks = model.Notifications.Select((req) => SendAsync(model.PrivateKey, req, ct)).ToArray();
                var responses =GetResponses(model.Notifications.Count);
                await Task.WhenAll(tasks);
                _responseChannel.Writer.Complete();
                return Ok(await responses);
            }
            catch (Exception ex)
            {
                _responseChannel.Writer.TryComplete();
                _requestSemaphore.Dispose();
                _logger.LogError(ex, "Sending Push Notifications", model);
                return BadRequest(ex.Message);
            }
        }

        private async Task SendAsync(byte[] privateKey, NotificationModel model, CancellationToken ct = default) {
            await _requestSemaphore.WaitAsync();
            try { 
                 var resp = await _apnService.SendAsync(privateKey, model, ct);
                _responseChannel.Writer.TryWrite(resp);
                await Task.CompletedTask;
            }catch (CryptographicException ex) {
                _logger.LogError("bad", ex);
                throw ;
            } finally {
                _requestSemaphore.Release();
            }
        }
     
        private async Task<PushServiceResult<ApnsResponse>[]> GetResponses(int size) {
            var results = new PushServiceResult<ApnsResponse>[size];
            int index = 0;
            while (await _responseChannel.Reader.WaitToReadAsync()){
                if (_responseChannel.Reader.TryRead(out var response)){             
                    results[index++] = response;
                }
            }
            return results;
        }
    }
}