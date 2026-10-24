using BlinkyAccess.Client;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mRemoteNGTests.BlinkyAccess
{
    /// <summary>
    /// The client talks to a stub instead of a portal, so the request shape, the wire
    /// format and the error handling can be checked without an environment.
    /// </summary>
    public class BlinkyAccessClientTests
    {
        private const string Token = "bla_pat_0123456789abcdef";

        /// <summary>
        /// The documented response, with a field this version does not know about, to
        /// show that a portal may add fields without breaking the import.
        /// </summary>
        private const string SampleResponse = """
        {
          "format": "blinky-access-connections/1",
          "connections": [
            {
              "id": "2f1b8c64-5d3a-4b7e-9f10-6a2c8e4d1b33",
              "name": "szymon@app-01",
              "folder": "Production",
              "hostname": "app-01.ad.digitalworkspace.pl",
              "port": 2222,
              "protocol": "SSH2",
              "username": "szymon",
              "smartcardCertificate": "FIDO:ssh:blinky",
              "sshAgentForwarding": true,
              "description": "Application server",
              "aFieldAddedLaterByThePortal": 42
            },
            {
              "id": "7c9d2e18-4a6b-4c3d-8e5f-1b2a3c4d5e6f",
              "name": "root@db-01",
              "folder": "",
              "hostname": "db-01.ad.digitalworkspace.pl",
              "protocol": "SSH2",
              "username": "root",
              "sshAgentForwarding": false,
              "description": ""
            }
          ]
        }
        """;

        private static BlinkyAccessOptions Options() => new()
        {
            PortalUrl = "https://bla-portal.ad.digitalworkspace.pl",
            Token = Token
        };

        private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = [];

            protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return responder(request);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Send(request, cancellationToken));
            }
        }

        private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        #region request shape

        [Test]
        public void TheConnectionsEndpointIsBuiltFromThePortalAddress()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new BlinkyAccessOptions { PortalUrl = "https://portal.example.com" }.ConnectionsUrl.AbsoluteUri,
                    Is.EqualTo("https://portal.example.com/api/user/v1/connections"));
                Assert.That(new BlinkyAccessOptions { PortalUrl = "https://portal.example.com/" }.ConnectionsUrl.AbsoluteUri,
                    Is.EqualTo("https://portal.example.com/api/user/v1/connections"));
                Assert.That(new BlinkyAccessOptions { PortalUrl = "portal.example.com" }.ConnectionsUrl.AbsoluteUri,
                    Is.EqualTo("https://portal.example.com/api/user/v1/connections"));
            });
        }

        [Test]
        public void TheEndpointIsNotAppendedTwiceToAnAddressThatAlreadyCarriesIt()
        {
            // the documentation shows the endpoint, so that is what gets pasted into
            // the address field; appending the path again produced a 404
            Assert.Multiple(() =>
            {
                Assert.That(new BlinkyAccessOptions { PortalUrl = "https://portal.example.com/api/user/v1/connections" }.ConnectionsUrl.AbsoluteUri,
                    Is.EqualTo("https://portal.example.com/api/user/v1/connections"));
                Assert.That(new BlinkyAccessOptions { PortalUrl = "https://portal.example.com/api/user/v1/connections/" }.ConnectionsUrl.AbsoluteUri,
                    Is.EqualTo("https://portal.example.com/api/user/v1/connections"));
                Assert.That(new BlinkyAccessOptions { PortalUrl = "portal.example.com/API/USER/V1/CONNECTIONS" }.ConnectionsUrl.AbsoluteUri,
                    Is.EqualTo("https://portal.example.com/API/USER/V1/CONNECTIONS"));
            });
        }

        [Test]
        public void APortalAddressThatIsNotHttpsIsRefusedBeforeAnythingIsSent()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<BlinkyAccessConfigurationException>(
                    () => _ = new BlinkyAccessOptions { PortalUrl = "http://portal.example.com" }.ConnectionsUrl);
                Assert.Throws<BlinkyAccessConfigurationException>(
                    () => _ = new BlinkyAccessOptions { PortalUrl = "" }.ConnectionsUrl);
                Assert.That(BlinkyAccessOptions.IsUsablePortalUrl("http://portal.example.com"), Is.False);
                Assert.That(BlinkyAccessOptions.IsUsablePortalUrl("https://portal.example.com"), Is.True);
            });
        }

        [Test]
        public void TheTokenTravelsInTheAuthorizationHeaderAndNeverInTheUrl()
        {
            StubHandler handler = new(_ => Json(SampleResponse));
            using BlinkyAccessClient client = new(Options(), handler);

            client.GetConnections();

            HttpRequestMessage request = handler.Requests.Single();
            Assert.Multiple(() =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(request.Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
                Assert.That(request.Headers.Authorization.Parameter, Is.EqualTo(Token));
                Assert.That(request.RequestUri!.AbsoluteUri,
                    Is.EqualTo("https://bla-portal.ad.digitalworkspace.pl/api/user/v1/connections"));
                Assert.That(request.RequestUri.Query, Is.Empty);
            });
        }

        [Test]
        public void NoCallIsMadeWithoutAToken()
        {
            StubHandler handler = new(_ => Json(SampleResponse));
            BlinkyAccessOptions options = Options();
            options.Token = "";
            using BlinkyAccessClient client = new(options, handler);

            Assert.Throws<BlinkyAccessConfigurationException>(() => client.GetConnections());
            Assert.That(handler.Requests, Is.Empty);
        }

        #endregion

        #region parsing

        [Test]
        public void TheDocumentedResponseIsReadIntoConnections()
        {
            IReadOnlyList<BlinkyAccessConnection> connections = BlinkyAccessClient.ParseConnectionsResponse(SampleResponse);

            Assert.That(connections, Has.Count.EqualTo(2));

            BlinkyAccessConnection first = connections[0];
            Assert.Multiple(() =>
            {
                Assert.That(first.Id, Is.EqualTo("2f1b8c64-5d3a-4b7e-9f10-6a2c8e4d1b33"));
                Assert.That(first.Name, Is.EqualTo("szymon@app-01"));
                Assert.That(first.Folder, Is.EqualTo("Production"));
                Assert.That(first.Hostname, Is.EqualTo("app-01.ad.digitalworkspace.pl"));
                Assert.That(first.Port, Is.EqualTo(2222));
                Assert.That(first.Protocol, Is.EqualTo("SSH2"));
                Assert.That(first.Username, Is.EqualTo("szymon"));
                Assert.That(first.SmartcardCertificate, Is.EqualTo("FIDO:ssh:blinky"));
                Assert.That(first.SshAgentForwarding, Is.True);
                Assert.That(first.Description, Is.EqualTo("Application server"));
            });

            BlinkyAccessConnection second = connections[1];
            Assert.Multiple(() =>
            {
                Assert.That(second.Folder, Is.Empty);
                Assert.That(second.Port, Is.Null, "a missing port is left for the protocol default");
                Assert.That(second.SmartcardCertificate, Is.Empty);
                Assert.That(second.SshAgentForwarding, Is.False);
            });
        }

        [Test]
        public void AnotherFormatVersionIsRefusedInsteadOfBeingGuessedAt()
        {
            const string body = """
            { "format": "blinky-access-connections/2", "connections": [] }
            """;

            BlinkyAccessUnsupportedFormatException ex =
                Assert.Throws<BlinkyAccessUnsupportedFormatException>(() => BlinkyAccessClient.ParseConnectionsResponse(body));

            Assert.That(ex.ReportedFormat, Is.EqualTo("blinky-access-connections/2"));
        }

        [Test]
        public void AResponseWithoutAFormatIsRefused()
        {
            Assert.Throws<BlinkyAccessUnsupportedFormatException>(
                () => BlinkyAccessClient.ParseConnectionsResponse("""{ "connections": [] }"""));
        }

        [Test]
        public void SomethingThatIsNotJsonIsReportedAsSuch()
        {
            BlinkyAccessException ex = Assert.Throws<BlinkyAccessException>(
                () => BlinkyAccessClient.ParseConnectionsResponse("<html>sign in</html>"));

            Assert.That(ex, Is.Not.InstanceOf<BlinkyAccessUnsupportedFormatException>());
        }

        #endregion

        #region failures

        [Test]
        public void ARefusedTokenIsReportedAsItsOwnExceptionSoItIsNotRetried()
        {
            StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("") });
            using BlinkyAccessClient client = new(Options(), handler);

            Assert.Throws<BlinkyAccessUnauthorizedException>(() => client.GetConnections());
            Assert.That(handler.Requests, Has.Count.EqualTo(1), "a refused token must not be retried");
        }

        [Test]
        public void ARateLimitCarriesTheWaitThePortalAskedFor()
        {
            StubHandler handler = new(_ =>
            {
                HttpResponseMessage response = new(HttpStatusCode.TooManyRequests) { Content = new StringContent("") };
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
                return response;
            });
            using BlinkyAccessClient client = new(Options(), handler);

            BlinkyAccessThrottledException ex = Assert.Throws<BlinkyAccessThrottledException>(() => client.GetConnections());

            Assert.Multiple(() =>
            {
                Assert.That(ex.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(30)));
                Assert.That(BlinkyAccessClient.Explain(ex), Does.Contain("30"));
            });
        }

        [Test]
        public void ARateLimitWithoutARetryAfterIsStillReportedAsOne()
        {
            StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("") });
            using BlinkyAccessClient client = new(Options(), handler);

            BlinkyAccessThrottledException ex = Assert.Throws<BlinkyAccessThrottledException>(() => client.GetConnections());

            Assert.That(ex.RetryAfter, Is.Null);
        }

        [Test]
        public void AnotherFailureKeepsItsStatusCodeAndLeavesTheBodyOutOfTheMessage()
        {
            StubHandler handler = new(_ => Json("the portal said something private", HttpStatusCode.InternalServerError));
            using BlinkyAccessClient client = new(Options(), handler);

            BlinkyAccessException ex = Assert.Throws<BlinkyAccessException>(() => client.GetConnections());

            Assert.Multiple(() =>
            {
                Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
                Assert.That(ex.Message, Does.Contain("500"));
                Assert.That(ex.Message, Does.Not.Contain("private"));
                Assert.That(ex.Message, Does.Not.Contain(Token));
            });
        }

        [Test]
        public void TheTestCallReportsSuccessAndFailureWithoutThrowingOrNamingTheToken()
        {
            StubHandler ok = new(_ => Json(SampleResponse));
            using BlinkyAccessClient workingClient = new(Options(), ok);
            BlinkyAccessTestResult success = workingClient.TestConnection();

            StubHandler refused = new(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("") });
            using BlinkyAccessClient refusedClient = new(Options(), refused);
            BlinkyAccessTestResult failure = refusedClient.TestConnection();

            Assert.Multiple(() =>
            {
                Assert.That(success.Success, Is.True);
                Assert.That(success.ConnectionCount, Is.EqualTo(2));
                Assert.That(failure.Success, Is.False);
                Assert.That(failure.Detail, Does.Contain("token"));
                Assert.That(failure.Detail, Does.Not.Contain(Token));
            });
        }

        #endregion
    }
}
