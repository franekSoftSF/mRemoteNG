using mRemoteNG.Config.Serializers.ConnectionSerializers.Xml;
using mRemoteNG.Connection;
using mRemoteNG.Security;
using mRemoteNG.Security.SymmetricEncryption;
using mRemoteNG.Tree;
using mRemoteNG.Tree.Root;
using NUnit.Framework;
using System.Linq;

namespace mRemoteNGTests.Config.Serializers
{
    /// <summary>
    /// Saving and loading the connection file has to keep the smart card settings,
    /// otherwise the value looks like it disappears right after being entered.
    /// </summary>
    public class SmartcardCertificateXmlRoundTripTests
    {
        private const string Identifier = "CAPI:1AC8798C0F78BF6DA810E712EBFB95B8C7D539D5";

        [Test]
        public void SmartcardSettingsSurviveSaveAndLoad()
        {
            ConnectionTreeModel model = new();
            RootNodeInfo root = new(RootNodeType.Connection);
            model.AddRootNode(root);

            ConnectionInfo connection = new()
            {
                Name = "lx-client01",
                Hostname = "lx-client01.example.com",
                Protocol = mRemoteNG.Connection.Protocol.ProtocolType.SSH2,
                SmartcardCertificate = Identifier,
                SshAgentForwarding = true
            };
            root.AddChild(connection);

            ICryptographyProvider cryptographyProvider = new AeadCryptographyProvider();
            XmlConnectionSerializerFactory factory = new();
            string xml = factory.Build(cryptographyProvider, model).Serialize(model.RootNodes.OfType<RootNodeInfo>().First());

            Assert.That(xml, Does.Contain("SmartcardCertificate=\"" + Identifier + "\""), "the value is not written to the file");

            XmlConnectionsDeserializer deserializer = new();
            ConnectionTreeModel loaded = deserializer.Deserialize(xml);
            ConnectionInfo loadedConnection = loaded.GetRecursiveChildList().First(c => c.Name == "lx-client01");

            Assert.Multiple(() =>
            {
                Assert.That(loadedConnection.SmartcardCertificate, Is.EqualTo(Identifier));
                Assert.That(loadedConnection.SshAgentForwarding, Is.True);
            });
        }
    }
}
