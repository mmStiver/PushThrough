using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dotAPNS;
using Microsoft.Extensions.Logging;
using Pushthrough.Web.Controllers;
using Pushthrough.Web.Services.Interface;
using Pushthrough.Web.Services;
using Pushthrough.Web.Models;
using Moq;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using System.Security.Cryptography;
using System.Threading;

namespace Pushthrough.Test {
    public static class TestDataGenerator {

        
         public static string GenerateString(int size) {
            var characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var Charsarr = new char[size];
            var random = new Random();

            for (int i = 0; i < Charsarr.Length; i++)
            {
                Charsarr[i] = characters[random.Next(characters.Length)];
            }

            return new String(Charsarr);
         }

        private static readonly string DeviceToken = "ORQFsuH2FkCOj1CSvD90De9aLUS5TQt9y5O1p9ZifDxOzPm7T0";
        private static readonly string BadToken = "EspcDRVtz5DhQ0LrGRdHzj2bc82c14brUvV4TOXg";
        public static class APNSTestData {
            public static ApnsPushRequestModel BuildRequestModel(byte[] privateKey, int success, int badToken) {
                var model = new ApnsPushRequestModel(){ PrivateKey = privateKey  };
                model.Notifications.AddRange(Enumerable.Range(0, success).Select(i => new NotificationModel() { DeviceToken = TestDataGenerator.DeviceToken, Title = TestDataGenerator.GenerateString(5), Text = TestDataGenerator.GenerateString(25), Sound = TestDataGenerator.GenerateString(3) }));
                model.Notifications.AddRange(Enumerable.Range(0, badToken).Select(i => new NotificationModel() { DeviceToken = TestDataGenerator.BadToken, Title = TestDataGenerator.GenerateString(5), Text = TestDataGenerator.GenerateString(25), Sound = TestDataGenerator.GenerateString(3) }));
                
                return model;
            }
             public static IApnsPassthroughController GetController() {
                var mockClient = new Mock<IApnsClient>();
                mockClient.Setup(client => client.SendAsync(It.Is<ApplePush>(alert => alert.Token == TestDataGenerator.DeviceToken), CancellationToken.None )).Returns(Task<ApnsResponse>.FromResult(ApnsResponse.Successful()));
                mockClient.Setup(client => client.SendAsync(It.Is<ApplePush>(alert => alert.Token == TestDataGenerator.BadToken), CancellationToken.None )).Returns(Task<ApnsResponse>.FromResult(ApnsResponse.Error(ApnsResponseReason.BadDeviceToken, "BadDeviceToken")));
                ;
                IPushNotificationService<PushServiceResult<ApnsResponse>> service  = new ApnsPushNotificationService(mockClient.Object, 
                    new Mock<ILogger<ApnsPushNotificationService>>().Object);
                IApnsPassthroughController controller = new ApnsPassthroughController(new Mock<ILogger<ApnsPassthroughController>>().Object, service);
                return controller;

            }
            public static IApnsPassthroughController GetController(X509Certificate2 cert) {
                IApnsClient client = ApnsClient.CreateUsingCert(cert);
                IPushNotificationService<PushServiceResult<ApnsResponse>> service  = new ApnsPushNotificationService(client, new Mock<Logger<ApnsPushNotificationService>>().Object);
                IApnsPassthroughController controller = new ApnsPassthroughController(new Mock<Logger<ApnsPassthroughController>>().Object, service, 100, 10);
                return controller;

            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "I don\'t care")]
        public static X509Certificate2 BuildSelfSignedServerCertificate(string CertificateName, string password)
        {
            SubjectAlternativeNameBuilder sanBuilder = new();
            sanBuilder.AddIpAddress(IPAddress.Loopback);
            sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName(Environment.MachineName);

            X500DistinguishedName distinguishedName = new($"CN={CertificateName}");

            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature , false));


                request.CertificateExtensions.Add(
                   new X509EnhancedKeyUsageExtension(
                       new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

                request.CertificateExtensions.Add(sanBuilder.Build());

                var certificate= request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));
                certificate.FriendlyName = CertificateName;

                return new X509Certificate2(certificate.Export(X509ContentType.Pfx, password), password, X509KeyStorageFlags.MachineKeySet);
            }
        }
    }
}
