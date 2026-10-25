using mRemoteNG.Config.Serializers.ConnectionSerializers.Xml;
using mRemoteNG.Connection;
using mRemoteNG.Container;
using mRemoteNG.Security;
using mRemoteNG.Security.SymmetricEncryption;
using mRemoteNG.Tree;
using mRemoteNG.Tree.Root;
using NUnit.Framework;
using System.Linq;

namespace mRemoteNGTests.Config.Serializers
{
    /// <summary>
    /// A folder has to come back from the connection file with the identifier it was
    /// saved with. Anything that keeps track of a folder by its id — a managed branch,
    /// an importer matching an existing tree — otherwise fails to recognise it after a
    /// restart and makes a second one beside it.
    /// </summary>
    public class ContainerIdXmlRoundTripTests
    {
        private const string ContainerId = "6b1c0a7e-8f2d-4a3c-9c51-b11c4acce55a";

        private static string Serialize(ConnectionTreeModel model)
        {
            ICryptographyProvider cryptographyProvider = new AeadCryptographyProvider();
            XmlConnectionSerializerFactory factory = new();

            return factory.Build(cryptographyProvider, model).Serialize(model.RootNodes.OfType<RootNodeInfo>().First());
        }

        [Test]
        public void AContainerKeepsItsIdentifierAcrossSaveAndLoad()
        {
            ConnectionTreeModel model = new();
            RootNodeInfo root = new(RootNodeType.Connection);
            model.AddRootNode(root);

            ContainerInfo folder = new(ContainerId) { Name = "Managed folder" };
            folder.AddChild(new ConnectionInfo { Name = "server-01", Hostname = "server-01.example.com" });
            root.AddChild(folder);

            string xml = Serialize(model);
            Assert.That(xml, Does.Contain($"Id=\"{ContainerId}\""), "the id is not written to the file");

            ConnectionTreeModel loaded = new XmlConnectionsDeserializer().Deserialize(xml);
            ContainerInfo loadedFolder = loaded.GetRecursiveChildList().OfType<ContainerInfo>().First(c => c.Name == "Managed folder");

            Assert.That(loadedFolder.ConstantID, Is.EqualTo(ContainerId));
        }

        [Test]
        public void TwoSaveAndLoadRoundsLeaveTheIdentifierAlone()
        {
            ConnectionTreeModel model = new();
            RootNodeInfo root = new(RootNodeType.Connection);
            model.AddRootNode(root);
            root.AddChild(new ContainerInfo(ContainerId) { Name = "Managed folder" });

            ConnectionTreeModel loaded = new XmlConnectionsDeserializer().Deserialize(Serialize(model));
            ConnectionTreeModel reloaded = new XmlConnectionsDeserializer().Deserialize(Serialize(loaded));

            ContainerInfo folder = reloaded.GetRecursiveChildList().OfType<ContainerInfo>().First(c => c.Name == "Managed folder");

            Assert.That(folder.ConstantID, Is.EqualTo(ContainerId));
        }
    }
}
