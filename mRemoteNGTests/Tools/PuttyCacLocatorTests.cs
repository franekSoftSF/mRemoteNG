using mRemoteNG.Tools;
using NUnit.Framework;
using System.IO;

namespace mRemoteNGTests.Tools
{
    public class PuttyCacLocatorTests
    {
        [Test]
        public void TheBundledPuttyNgIsNeverOfferedAsACertificateCapableClient()
        {
            string? found = PuttyCacLocator.Find();

            // whatever this machine has installed, the answer must not be PuTTYNG:
            // it has no certificate support, which is the whole point of looking
            Assert.That(found is null || !Path.GetFileName(found).Equals("PuTTYNG.exe", System.StringComparison.OrdinalIgnoreCase),
                $"PuTTYNG was returned as a certificate capable client: {found}");
        }

        [Test]
        public void AFoundClientExists()
        {
            string? found = PuttyCacLocator.Find();

            Assert.That(found is null || File.Exists(found), $"a path was returned that does not exist: {found}");
        }
    }
}
