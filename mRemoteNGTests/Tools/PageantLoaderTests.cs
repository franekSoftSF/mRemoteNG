using mRemoteNG.Connection.Protocol;
using mRemoteNG.Tools;
using NUnit.Framework;
using System;
using System.IO;

namespace mRemoteNGTests.Tools
{
    /// <summary>
    /// A connection carrying a certificate is switched from the bundled PuTTYNG to a
    /// PuTTY CAC found elsewhere, and the certificate then has to go into that
    /// installation's Pageant. Looking next to the configured client instead finds no
    /// pageant.exe at all, and agent forwarding silently forwards nothing.
    /// </summary>
    public class PageantLoaderTests
    {
        private string _directory;
        private string _pageant;
        private string _configuredPuttyBefore;

        [SetUp]
        public void Setup()
        {
            _configuredPuttyBefore = PuttyBase.PuttyPath;

            _directory = Path.Combine(Path.GetTempPath(), "mRemoteNGTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);

            _pageant = Path.Combine(_directory, "pageant.exe");
            File.WriteAllText(_pageant, "");
        }

        [TearDown]
        public void TearDown()
        {
            PuttyBase.PuttyPath = _configuredPuttyBefore;

            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
                // a temporary directory left behind must not fail the run
            }
        }

        [Test]
        public void ThePageantOfTheClientBeingRunIsPreferredOverTheConfiguredOne()
        {
            PuttyBase.PuttyPath = Path.Combine(Path.GetTempPath(), "no-pageant-here", "PuTTYNG.exe");

            string found = PageantLoader.GetPageantPath(Path.Combine(_directory, "putty.exe"));

            Assert.That(found, Is.EqualTo(_pageant));
        }

        [Test]
        public void WithoutAClientTheConfiguredPuttyStandsIn()
        {
            PuttyBase.PuttyPath = Path.Combine(_directory, "putty.exe");

            Assert.Multiple(() =>
            {
                Assert.That(PageantLoader.GetPageantPath(null), Is.EqualTo(_pageant));
                Assert.That(PageantLoader.GetPageantPath(""), Is.EqualTo(_pageant));
            });
        }

        [Test]
        public void NoPageantBesideEitherClientIsReportedAsNothingFound()
        {
            PuttyBase.PuttyPath = Path.Combine(Path.GetTempPath(), "no-pageant-here", "PuTTYNG.exe");

            string found = PageantLoader.GetPageantPath(Path.Combine(Path.GetTempPath(), "nor-here", "putty.exe"));

            Assert.That(found, Is.Null);
        }
    }
}
