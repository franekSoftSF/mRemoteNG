using BeyondTrust.PasswordSafe.Client;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mRemoteNGTests.BeyondTrust
{
    /// <summary>
    /// The client talks to a stub instead of a Password Safe instance, so the request
    /// shapes and the error handling can be checked without an environment.
    /// </summary>
    public class PasswordSafeClientTests
    {
        private const string ApiKey = "test-api-key";

        private static PasswordSafeOptions Options() => new()
        {
            Url = "https://ps.example.com/BeyondTrust",
            ApiKey = ApiKey,
            RunAsUser = "domain\\svc_mremoteng"
        };

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

            public List<HttpRequestMessage> Requests { get; } = [];
            public List<string> Bodies { get; } = [];

            protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                Bodies.Add(request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? "");
                return _responder(request);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Send(request, cancellationToken));
            }
        }

        private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        [Test]
        public void ApiBaseIsAppendedToAnInstallationRootButNotToAFullApiPath()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new PasswordSafeOptions { Url = "https://ps.example.com/BeyondTrust" }.ApiBase,
                    Is.EqualTo("https://ps.example.com/BeyondTrust/api/public/v3"));
                Assert.That(new PasswordSafeOptions { Url = "https://ps.example.com/BeyondTrust/api/public/v3/" }.ApiBase,
                    Is.EqualTo("https://ps.example.com/BeyondTrust/api/public/v3"));
            });
        }

        [Test]
        public void SignInSendsThePsAuthHeaderAndMarksTheSessionAsOpen()
        {
            StubHandler handler = new(_ => Json("\"ok\""));
            using PasswordSafeClient client = new(Options(), handler);

            client.SignIn();

            string authorization = handler.Requests[0].Headers.GetValues("Authorization").Single();
            Assert.Multiple(() =>
            {
                Assert.That(client.IsSignedIn, Is.True);
                Assert.That(handler.Requests[0].RequestUri!.AbsoluteUri,
                    Is.EqualTo("https://ps.example.com/BeyondTrust/api/public/v3/Auth/SignAppIn"));
                Assert.That(authorization, Does.StartWith($"PS-Auth key={ApiKey}; runas=domain\\svc_mremoteng;"));
            });
        }

        [Test]
        public void SignInIncludesThePasswordAndChallengeOnlyWhenTheyAreSet()
        {
            PasswordSafeOptions options = Options();
            options.RunAsPassword = "s3cret";
            options.Challenge = "123456";

            StubHandler handler = new(_ => Json("\"ok\""));
            using PasswordSafeClient client = new(options, handler);

            client.SignIn();

            string authorization = handler.Requests[0].Headers.GetValues("Authorization").Single();
            Assert.Multiple(() =>
            {
                Assert.That(authorization, Does.Contain("pwd=[s3cret];"));
                Assert.That(authorization, Does.Contain("challenge=123456;"));
            });
        }

        [Test]
        public void SignInReportsASecondFactorPromptAsItsOwnException()
        {
            StubHandler handler = new(_ =>
            {
                HttpResponseMessage response = new(HttpStatusCode.Unauthorized) { Content = new StringContent("") };
                response.Headers.TryAddWithoutValidation("WWW-Authenticate-2FA", "Enter your RADIUS code");
                return response;
            });
            using PasswordSafeClient client = new(Options(), handler);

            PasswordSafeChallengeRequiredException ex =
                Assert.Throws<PasswordSafeChallengeRequiredException>(() => client.SignIn());

            Assert.Multiple(() =>
            {
                Assert.That(ex.Prompt, Is.EqualTo("Enter your RADIUS code"));
                Assert.That(client.IsSignedIn, Is.False);
            });
        }

        [Test]
        public void SignInFailureCarriesTheStatusCode()
        {
            StubHandler handler = new(_ => Json("\"bad key\"", HttpStatusCode.Unauthorized));
            using PasswordSafeClient client = new(Options(), handler);

            PasswordSafeException ex = Assert.Throws<PasswordSafeException>(() => client.SignIn());

            Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public void ListManagedAccountsResolvesHostPortAndPlatform()
        {
            StubHandler handler = new(request =>
            {
                string url = request.RequestUri!.AbsoluteUri;

                if (url.Contains("/ManagedAccounts?") && url.Contains("offset=0"))
                    return Json("""
                        [{"AccountId":7,"AccountName":"root","SystemId":3,"SystemName":"lx-client01","PlatformID":11,
                          "MaximumReleaseDuration":120,"DefaultReleaseDuration":30}]
                        """);

                if (url.Contains("/ManagedAccounts?"))
                    return Json("[]");

                if (url.Contains("/ManagedSystems?") && url.Contains("offset=0"))
                    return Json("""
                        {"TotalCount":1,"Data":[{"ManagedSystemID":3,"SystemName":"lx-client01",
                          "DnsName":"lx-client01.ad.example.com","HostName":"lx-client01","PlatformID":11}]}
                        """);

                if (url.Contains("/ManagedSystems?"))
                    return Json("""{"TotalCount":0,"Data":[]}""");

                if (url.EndsWith("/Platforms"))
                    return Json("""[{"PlatformID":11,"Name":"Linux","PortFlag":true,"DefaultPort":22}]""");

                return Json("[]");
            });

            using PasswordSafeClient client = new(Options(), handler);
            ManagedAccount account = client.ListManagedAccounts().Single();

            Assert.Multiple(() =>
            {
                Assert.That(account.Host, Is.EqualTo("lx-client01.ad.example.com"));
                Assert.That(account.Port, Is.EqualTo(22));
                Assert.That(account.PlatformName, Is.EqualTo("Linux"));
                Assert.That(account.Id, Is.EqualTo("3:7"));
                Assert.That(account.ReleaseDurationMinutes, Is.EqualTo(30));
            });
        }

        [Test]
        public void ListManagedAccountsSurvivesPlatformsBeingUnavailable()
        {
            StubHandler handler = new(request =>
            {
                string url = request.RequestUri!.AbsoluteUri;

                if (url.Contains("/ManagedAccounts?") && url.Contains("offset=0"))
                    return Json("""[{"AccountId":1,"AccountName":"admin","SystemId":2,"SystemName":"dc01"}]""");
                if (url.Contains("/ManagedAccounts?"))
                    return Json("[]");
                if (url.Contains("/ManagedSystems?"))
                    return Json("""{"TotalCount":0,"Data":[]}""");

                return Json("\"no access\"", HttpStatusCode.Forbidden);
            });

            using PasswordSafeClient client = new(Options(), handler);
            ManagedAccount account = client.ListManagedAccounts().Single();

            Assert.Multiple(() =>
            {
                Assert.That(account.PlatformName, Is.Empty);
                Assert.That(account.Host, Is.EqualTo("dc01"), "falls back to the system name");
            });
        }

        [Test]
        public void ReleaseCreatesARequestAndReadsTheCredential()
        {
            StubHandler handler = new(request =>
                request.RequestUri!.AbsoluteUri.EndsWith("/Requests")
                    ? Json("4242", HttpStatusCode.Created)
                    : Json("\"hunter2\""));

            using PasswordSafeClient client = new(Options(), handler);

            ManagedAccount account = new()
            {
                AccountId = 7,
                SystemId = 3,
                AccountName = "root",
                DomainName = "ad.example.com",
                DefaultReleaseDuration = 30
            };

            ReleasedCredential credential = client.Release(account, "mRemoteNG connection");

            Assert.Multiple(() =>
            {
                Assert.That(credential.RequestId, Is.EqualTo(4242));
                Assert.That(credential.Password, Is.EqualTo("hunter2"));
                Assert.That(credential.Username, Is.EqualTo("root"));
                Assert.That(credential.Domain, Is.EqualTo("ad.example.com"));
                Assert.That(handler.Bodies[0], Does.Contain("\"AccountID\":7").And.Contain("\"SystemID\":3"));
                Assert.That(handler.Bodies[0], Does.Contain("\"DurationMinutes\":30"));
                Assert.That(handler.Bodies[0], Does.Contain("\"ConflictOption\":\"reuse\""),
                    "an existing release of the same account must be reused, not duplicated");
                Assert.That(handler.Requests[1].RequestUri!.AbsoluteUri, Does.EndWith("/Credentials/4242"));
            });
        }

        [Test]
        public void AFailedCredentialReadNeverCarriesTheResponseBody()
        {
            // the body of this endpoint is the secret itself, so it must not end up in
            // an exception message that could be logged
            StubHandler handler = new(_ => Json("\"hunter2\"", HttpStatusCode.Forbidden));
            using PasswordSafeClient client = new(Options(), handler);

            PasswordSafeException ex = Assert.Throws<PasswordSafeAccessDeniedException>(() => client.GetCredential(1));

            Assert.Multiple(() =>
            {
                Assert.That(ex.Message, Does.Not.Contain("hunter2"));
                Assert.That(ex.ResponseBody, Is.Null);
                Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            });
        }

        [Test]
        public void CheckInPutsToTheRequest()
        {
            StubHandler handler = new(_ => Json(""));
            using PasswordSafeClient client = new(Options(), handler);

            client.CheckIn(4242, "done");

            Assert.Multiple(() =>
            {
                Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Put));
                Assert.That(handler.Requests[0].RequestUri!.AbsoluteUri, Does.EndWith("/Requests/4242/Checkin"));
                Assert.That(handler.Bodies[0], Does.Contain("done"));
            });
        }

        [Test]
        public void AnAccessDeniedResponseIsDistinguishableFromOtherFailures()
        {
            StubHandler handler = new(_ => Json("\"4031 user cannot request\"", HttpStatusCode.Forbidden));
            using PasswordSafeClient client = new(Options(), handler);

            Assert.Throws<PasswordSafeAccessDeniedException>(() => client.CreateRequest(3, 7, 30));
        }

        [Test]
        public void ConnectionTestReportsEveryCallOnASetupThatWorks()
        {
            StubHandler handler = new(request =>
            {
                string url = request.RequestUri!.AbsoluteUri;

                if (url.EndsWith("/Auth/SignAppIn")) return Json("\"ok\"");
                if (url.Contains("/ManagedAccounts?")) return Json("""[{"AccountId":7,"AccountName":"root","SystemId":3,"SystemName":"lx-client01"}]""");
                if (url.Contains("/ManagedSystems?")) return Json("""{"TotalCount":4,"Data":[]}""");
                if (url.EndsWith("/Platforms")) return Json("""[{"PlatformID":11,"Name":"Linux"}]""");

                return Json("[]");
            });

            using PasswordSafeClient client = new(Options(), handler);
            ConnectionTestResult result = client.TestConnection();

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.ToString());
                Assert.That(result.Steps, Has.Count.EqualTo(4));
                Assert.That(result.ToString(), Does.Contain("lx-client01\\root").And.Contain("4 system(s)"));
            });
        }

        [Test]
        public void ConnectionTestStopsAtSignInAndExplainsASecondFactor()
        {
            StubHandler handler = new(_ =>
            {
                HttpResponseMessage response = new(HttpStatusCode.Unauthorized) { Content = new StringContent("") };
                response.Headers.TryAddWithoutValidation("WWW-Authenticate-2FA", "Enter your RADIUS code");
                return response;
            });

            using PasswordSafeClient client = new(Options(), handler);
            ConnectionTestResult result = client.TestConnection();

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Steps, Has.Count.EqualTo(1), "the later calls are pointless without a session");
                Assert.That(result.ToString(), Does.Contain("two factor authentication required"));
            });
        }

        [Test]
        public void ConnectionTestPointsAtTheApiAccessFlagWhenNoAccountIsVisible()
        {
            StubHandler handler = new(request =>
                request.RequestUri!.AbsoluteUri.Contains("/ManagedAccounts?")
                    ? Json("[]")
                    : Json("""{"TotalCount":0,"Data":[]}"""));

            using PasswordSafeClient client = new(Options(), handler);
            ConnectionTestResult result = client.TestConnection();

            Assert.That(result.ToString(), Does.Contain("Enable for API Access"));
        }

        [TestCase("3:7", 3, 7)]
        [TestCase(" 10 : 20 ", 10, 20)]
        public void IdentifiersRoundTrip(string id, int systemId, int accountId)
        {
            (int parsedSystem, int parsedAccount) = ManagedAccount.ParseId(id);

            Assert.Multiple(() =>
            {
                Assert.That(parsedSystem, Is.EqualTo(systemId));
                Assert.That(parsedAccount, Is.EqualTo(accountId));
                Assert.That(new ManagedAccount { SystemId = systemId, AccountId = accountId }.Id, Is.EqualTo($"{systemId}:{accountId}"));
            });
        }

        [TestCase("")]
        [TestCase("7")]
        [TestCase("a:b")]
        public void AMalformedIdentifierIsRejected(string id)
        {
            Assert.Throws<FormatException>(() => ManagedAccount.ParseId(id));
        }
    }
}
