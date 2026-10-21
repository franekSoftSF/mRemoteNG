using mRemoteNG.Tools;
using NUnit.Framework;

namespace mRemoteNGTests.Tools
{
    public class SmartcardCertificateStoreTests
    {
        [TestCase("")]
        [TestCase(null)]
        [TestCase("PKCS:B8B58D8F2C3A7F98F3F645161B1BF9818B689716=C:\\PKCS.dll")]
        [TestCase("FIDO:ssh:MyFidoKey")]
        [TestCase("CAPI:1AC8798C0F78BF6DA810E712EBFB95B8C7D539D5")]
        public void NormalizeCapiIdentifierLeavesEverythingButSha256Untouched(string identifier)
        {
            Assert.That(SmartcardCertificateStore.NormalizeCapiIdentifier(identifier), Is.EqualTo(identifier));
        }

        [Test]
        public void NormalizeCapiIdentifierKeepsASha256OfAnUnknownCertificate()
        {
            // nothing in the store can match this, so the value has to survive and let
            // PuTTY report the problem rather than being silently rewritten
            const string identifier = "CAPI:0000000000000000000000000000000000000000000000000000000000000000";

            Assert.That(SmartcardCertificateStore.NormalizeCapiIdentifier(identifier), Is.EqualTo(identifier));
        }

        [Test]
        public void NormalizeCapiIdentifierIgnoresValuesThatAreNotHex()
        {
            const string identifier = "CAPI:not-a-thumbprint";

            Assert.That(SmartcardCertificateStore.NormalizeCapiIdentifier(identifier), Is.EqualTo(identifier));
        }
    }
}
