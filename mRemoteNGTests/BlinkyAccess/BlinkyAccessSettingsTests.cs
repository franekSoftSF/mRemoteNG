using mRemoteNG.Config.BlinkyAccess;
using NUnit.Framework;

namespace mRemoteNGTests.BlinkyAccess
{
    /// <summary>
    /// The token must never be readable from the settings, and the options page must
    /// never be able to put a whole one on screen.
    ///
    /// Only the in-memory settings are changed here; nothing calls Save, so the user's
    /// own stored token is left alone.
    /// </summary>
    public class BlinkyAccessSettingsTests
    {
        private const string Token = "bla_pat_0123456789abcdef";

        private string _storedBefore;

        [SetUp]
        public void Setup()
        {
            _storedBefore = mRemoteNG.Properties.OptionsBlinkyAccessPage.Default.ProtectedAccessToken;
        }

        [TearDown]
        public void TearDown()
        {
            mRemoteNG.Properties.OptionsBlinkyAccessPage.Default.ProtectedAccessToken = _storedBefore;
        }

        [Test]
        public void TheStoredTokenIsEncryptedAndComesBackAsItWentIn()
        {
            BlinkyAccessSettings.SetToken(Token);

            string stored = mRemoteNG.Properties.OptionsBlinkyAccessPage.Default.ProtectedAccessToken;
            Assert.Multiple(() =>
            {
                Assert.That(stored, Is.Not.Empty);
                Assert.That(stored, Does.Not.Contain(Token), "the settings must not hold the token itself");
                Assert.That(stored, Does.Not.Contain("bla_pat"));
                Assert.That(BlinkyAccessSettings.GetToken(), Is.EqualTo(Token));
                Assert.That(BlinkyAccessSettings.HasToken, Is.True);
            });
        }

        [Test]
        public void AnEmptyTokenClearsTheStoredOne()
        {
            BlinkyAccessSettings.SetToken(Token);

            BlinkyAccessSettings.SetToken("   ");

            Assert.Multiple(() =>
            {
                Assert.That(BlinkyAccessSettings.HasToken, Is.False);
                Assert.That(BlinkyAccessSettings.GetToken(), Is.Empty);
            });
        }

        [Test]
        public void OnlyTheBeginningOfATokenIsEverShown()
        {
            string preview = BlinkyAccessSettings.BuildPreview(Token);

            Assert.Multiple(() =>
            {
                Assert.That(preview, Does.StartWith("bla_pat_0123"));
                Assert.That(preview, Does.EndWith("..."));
                Assert.That(preview, Does.Not.Contain("456789abcdef"));
                Assert.That(BlinkyAccessSettings.BuildPreview(""), Is.Empty);
            });
        }

        [Test]
        public void AnUnreadableStoredValueIsDroppedInsteadOfFailingEveryCall()
        {
            mRemoteNG.Properties.OptionsBlinkyAccessPage.Default.ProtectedAccessToken = "not base64 at all";

            Assert.Multiple(() =>
            {
                Assert.That(BlinkyAccessSettings.GetToken(), Is.Empty);
                Assert.That(BlinkyAccessSettings.HasToken, Is.False);
            });
        }
    }
}
