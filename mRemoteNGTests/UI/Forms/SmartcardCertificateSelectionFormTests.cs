using mRemoteNG.UI.Forms;
using NUnit.Framework;
using System.Threading;

namespace mRemoteNGTests.UI.Forms
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class SmartcardCertificateSelectionFormTests
    {
        [Test]
        public void FormCanBeBuiltAndReadsTheCertificateStore()
        {
            using SmartcardCertificateSelectionForm form = new();

            Assert.That(form.SelectedIdentifier, Is.Empty);
        }

        [Test]
        public void FormKeepsTheCurrentIdentifierWhenNothingIsPicked()
        {
            const string identifier = "CAPI:1AC8798C0F78BF6DA810E712EBFB95B8C7D539D5";

            using SmartcardCertificateSelectionForm form = new(identifier);

            Assert.That(form.SelectedIdentifier, Is.EqualTo(identifier));
        }
    }
}
