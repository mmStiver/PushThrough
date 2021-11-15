using dotAPNS;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using Pushthrough.Web.Controllers;
using Pushthrough.Web.Models;
using Pushthrough.Web.Services;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Pushthrough.Test {
    [TestFixture]
    public class APNSPushTests {
        [SetUp]
        public void Setup() {
        }

        [TestFixture]
        public class APNSCertificateConnection : APNSPushTests{
            [TestCase]
              public async Task Send_EmptyNotificationList_ReturnErrorResponse() {
                //Assemble
                X509Certificate2 cert = TestDataGenerator.BuildSelfSignedServerCertificate("name", "pass");
                IApnsPassthroughController controller = TestDataGenerator.APNSTestData.GetController();
                var request = new ApnsPushRequestModel(){ PrivateKey = cert.RawData  };

                //Act
                var result = await controller.PushAsync(request, CancellationToken.None) as BadRequestObjectResult;

                //Assert
                Assert.IsNotNull(result)   ;
            }

              [TestCase]
              public async Task Send_EmptyDeviceToken_ReturnErrorResponse() {
                //Assemble
                X509Certificate2 cert = TestDataGenerator.BuildSelfSignedServerCertificate("name", "pass");
                IApnsPassthroughController controller = TestDataGenerator.APNSTestData.GetController();
                var request = new ApnsPushRequestModel(){ PrivateKey = cert.RawData  };
                foreach(var i in Enumerable.Range(0, 10))
                    request.Notifications.Add(new NotificationModel() { DeviceToken = string.Empty, Text = TestDataGenerator.GenerateString(10) });

                //Act
                var result = await controller.PushAsync(request, CancellationToken.None) as OkObjectResult;
                IEnumerable<PushServiceResult<ApnsResponse>> content = result.Value as IEnumerable<PushServiceResult<ApnsResponse>>;
                var li = content.ToList();

                //Assert
                Assert.That(li, Has.All.Matches<PushServiceResult<ApnsResponse>>(asdf=> asdf.Entity.IsSuccessful == false && asdf.Entity.Reason == ApnsResponseReason.MissingDeviceToken ))   ;
            }

            [TestCase]
              public async Task Send_EmptyNotificationText_ReturnErrorResponse() {
                //Assemble
                X509Certificate2 cert = TestDataGenerator.BuildSelfSignedServerCertificate("name", "pass");
                IApnsPassthroughController controller = TestDataGenerator.APNSTestData.GetController();
                var request = new ApnsPushRequestModel(){ PrivateKey = cert.RawData  };
                foreach(var i in Enumerable.Range(0, 10))
                    request.Notifications.Add(new NotificationModel() { DeviceToken = TestDataGenerator.GenerateString(10), Text =string.Empty });

                //Act
                var result = await controller.PushAsync(request, CancellationToken.None) as OkObjectResult;
                IEnumerable<PushServiceResult<ApnsResponse>> content = result.Value as IEnumerable<PushServiceResult<ApnsResponse>>;
                var li = content.ToList();

                //Assert
                Assert.That(li, Has.All.Matches<PushServiceResult<ApnsResponse>>(asdf=> asdf.Entity.IsSuccessful == false && asdf.Entity.Reason == ApnsResponseReason.PayloadEmpty ))   ;
            }
            [TestCase]
              public async Task Send_EmptyNotificationSound_ReturnErrorResponse() {
                //Assemble
                X509Certificate2 cert = TestDataGenerator.BuildSelfSignedServerCertificate("name", "pass");
                IApnsPassthroughController controller = TestDataGenerator.APNSTestData.GetController();
                var request = new ApnsPushRequestModel(){ PrivateKey = cert.RawData  };
                foreach(var i in Enumerable.Range(0, 10))
                    request.Notifications.Add(new NotificationModel() { DeviceToken = TestDataGenerator.GenerateString(10), Text=TestDataGenerator.GenerateString(10) });

                //Act
                var result = await controller.PushAsync(request, CancellationToken.None) as OkObjectResult;
                IEnumerable<PushServiceResult<ApnsResponse>> content = result.Value as IEnumerable<PushServiceResult<ApnsResponse>>;
                var li = content.ToList();

                //Assert
                Assert.That(li, Has.All.Matches<PushServiceResult<ApnsResponse>>(asdf=> asdf.Entity.IsSuccessful == false && asdf.Entity.Reason == ApnsResponseReason.InternalServerError ))   ;
            }
             [TestCase]
              public async Task Send_EmptyCert_ReturnErrorResponse() {
                //Assemble
                IApnsPassthroughController controller = TestDataGenerator.APNSTestData.GetController();
                var request = new ApnsPushRequestModel();
                request.Notifications.AddRange(Enumerable.Range(0, 10).Select(i => new NotificationModel() { DeviceToken = string.Empty }));

                //Act
                var result = await controller.PushAsync(request, CancellationToken.None) as BadRequestObjectResult;

                //Assert
                Assert.IsNotNull(result)   ;
            }


            [TestCase(1, 0)]
            [TestCase(0, 1)]
            [TestCase(1, 1)]
            [TestCase(10, 10)]
            [TestCase(10, 50)]
            [TestCase(50, 10)]
            [TestCase(100, 100)]
            [TestCase(0, 100)]
            [TestCase(100, 0)]
            [TestCase(500, 500)]
            public async Task Send_MobileNotificationRequest_ReturnAPNSResponse(int successCount, int errorCount) {
                //Assemble
                X509Certificate2 cert = TestDataGenerator.BuildSelfSignedServerCertificate("name", "pass");
                IApnsPassthroughController controller = TestDataGenerator.APNSTestData.GetController();
                var request = TestDataGenerator.APNSTestData.BuildRequestModel(cert.RawData, successCount, errorCount);

                //Act
                var result = await controller.PushAsync(request, CancellationToken.None) as OkObjectResult;
                IEnumerable<PushServiceResult<ApnsResponse>> content = result.Value as IEnumerable<PushServiceResult<ApnsResponse>>;
                var li = content.ToList();

                //Assert
                Assert.AreEqual(successCount, li.Where(res => res.Entity.IsSuccessful).Count()) ;
                Assert.AreEqual(errorCount, li.Where(res => !res.Entity.IsSuccessful).Count());
            }
        }

        [TestFixture]
        public class APNSJWTConnection {

        }
        
        [TestFixture]
        public class APNSMockedService {
            [Test]
            public void Test1() {
                Assert.Pass();
            }
        }
    }
}