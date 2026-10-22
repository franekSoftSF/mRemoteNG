using mRemoteNG.UI.Forms;
using NUnit.Framework;
using System.Diagnostics;
using System.Threading;

namespace mRemoteNGTests.UI.Forms
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class SmartcardConnectionFormTests
    {
        [Test]
        public void FormBuildsAndReadsTheCardWithoutBlocking()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            using SmartcardConnectionForm form = new();

            // reading the store must never open a private key: that makes Windows reach
            // for a card and can block for minutes behind a card selection prompt
            Assert.Multiple(() =>
            {
                Assert.That(form.Connection, Is.Null);
                Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000));
            });
        }
    }
}
