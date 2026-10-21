using mRemoteNG.Connection;
using mRemoteNG.Container;
using NUnit.Framework;

namespace mRemoteNGTests.Connection
{
    /// <summary>
    /// The smart card certificate is a plain string property, but it has to survive the
    /// inheritance machinery and a round trip through the connection file.
    /// </summary>
    public class SmartcardCertificatePropertyTests
    {
        private const string Identifier = "CAPI:1AC8798C0F78BF6DA810E712EBFB95B8C7D539D5";

        [Test]
        public void ValueIsKeptOnAConnectionWithoutAParent()
        {
            ConnectionInfo connection = new() { SmartcardCertificate = Identifier };

            Assert.That(connection.SmartcardCertificate, Is.EqualTo(Identifier));
        }

        [Test]
        public void ValueIsKeptOnAConnectionInsideAFolder()
        {
            ContainerInfo folder = new() { Name = "folder" };
            ConnectionInfo connection = new();
            folder.AddChild(connection);
            connection.Inheritance.SmartcardCertificate = false;

            connection.SmartcardCertificate = Identifier;

            Assert.That(connection.SmartcardCertificate, Is.EqualTo(Identifier));
        }

        /// <summary>
        /// Every connection property needs an InhDefault/ConDefault setting. Without
        /// one, the default inheritance object keeps whatever it happened to hold, the
        /// property silently inherits from the parent folder, and an entered value
        /// appears to vanish.
        /// </summary>
        [Test]
        public void DefaultSettingsExistAndTurnInheritanceOff()
        {
            Assert.Multiple(() =>
            {
                Assert.That(mRemoteNG.Properties.Settings.Default.InhDefaultSmartcardCertificate, Is.False);
                Assert.That(mRemoteNG.Properties.Settings.Default.InhDefaultSshAgentForwarding, Is.False);
                Assert.That(mRemoteNG.Properties.Settings.Default.ConDefaultSmartcardCertificate, Is.Empty);
                Assert.That(mRemoteNG.Properties.Settings.Default.ConDefaultSshAgentForwarding, Is.False);
            });
        }

        [Test]
        public void InheritedValueComesFromTheFolder()
        {
            ContainerInfo folder = new() { Name = "folder", SmartcardCertificate = Identifier };
            ConnectionInfo connection = new();
            folder.AddChild(connection);
            connection.Inheritance.SmartcardCertificate = true;

            Assert.That(connection.SmartcardCertificate, Is.EqualTo(Identifier));
        }

        [Test]
        public void AgentForwardingIsKept()
        {
            ConnectionInfo connection = new() { SshAgentForwarding = true };

            Assert.That(connection.SshAgentForwarding, Is.True);
        }
    }
}
