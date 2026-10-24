using BlinkyAccess.Client;
using mRemoteNG.Config.BlinkyAccess;
using mRemoteNG.Connection;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Container;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace mRemoteNGTests.BlinkyAccess
{
    /// <summary>
    /// The managed branch is built and kept in line here, so this covers the mapping
    /// onto a connection, the upsert by identifier, and that pruning stays inside the
    /// branch.
    /// </summary>
    public class BlinkyAccessTreeSynchronizerTests
    {
        private const string AppId = "2f1b8c64-5d3a-4b7e-9f10-6a2c8e4d1b33";
        private const string DbId = "7c9d2e18-4a6b-4c3d-8e5f-1b2a3c4d5e6f";

        private BlinkyAccessTreeSynchronizer _synchronizer;
        private ContainerInfo _root;

        [SetUp]
        public void Setup()
        {
            _synchronizer = new BlinkyAccessTreeSynchronizer();
            _root = new ContainerInfo { Name = "Connections" };
        }

        private static BlinkyAccessConnection AppConnection() => new()
        {
            Id = AppId,
            Name = "szymon@app-01",
            Folder = "Production",
            Hostname = "app-01.ad.digitalworkspace.pl",
            Port = 2222,
            Protocol = "SSH2",
            Username = "szymon",
            SmartcardCertificate = "FIDO:ssh:blinky",
            SshAgentForwarding = true,
            Description = "Application server"
        };

        private static BlinkyAccessConnection DbConnection() => new()
        {
            Id = DbId,
            Name = "root@db-01",
            Hostname = "db-01.ad.digitalworkspace.pl",
            Protocol = "SSH2",
            Username = "root"
        };

        private ContainerInfo Branch() => BlinkyAccessTreeSynchronizer.FindBranch(_root);

        private ConnectionInfo Find(string id) =>
            Branch().GetRecursiveChildList().FirstOrDefault(c => c.ConstantID == id);

        #region creating

        [Test]
        public void TheBranchIsCreatedWithItsOwnFolderPerPortalFolder()
        {
            BlinkyAccessSyncSummary summary = _synchronizer.Synchronize(_root, [AppConnection(), DbConnection()]);

            ContainerInfo branch = Branch();
            Assert.Multiple(() =>
            {
                Assert.That(summary, Is.EqualTo(new BlinkyAccessSyncSummary(2, 0, 0)));
                Assert.That(_root.Children, Has.Count.EqualTo(1));
                Assert.That(branch, Is.Not.Null);
                Assert.That(branch.ConstantID, Is.EqualTo(BlinkyAccessTreeSynchronizer.BranchId));
                Assert.That(branch.Description, Is.Not.Empty, "the branch explains that it is managed");
                Assert.That(Find(AppId).Parent.Name, Is.EqualTo("Production"));
                Assert.That(Find(DbId).Parent, Is.SameAs(branch), "no folder means straight into the branch");
            });
        }

        [Test]
        public void EveryFieldThePortalSuppliesIsWrittenOntoTheConnection()
        {
            _synchronizer.Synchronize(_root, [AppConnection()]);

            ConnectionInfo connection = Find(AppId);
            Assert.Multiple(() =>
            {
                Assert.That(connection.ConstantID, Is.EqualTo(AppId));
                Assert.That(connection.Name, Is.EqualTo("szymon@app-01"));
                Assert.That(connection.Hostname, Is.EqualTo("app-01.ad.digitalworkspace.pl"));
                Assert.That(connection.Port, Is.EqualTo(2222));
                Assert.That(connection.Protocol, Is.EqualTo(ProtocolType.SSH2));
                Assert.That(connection.Username, Is.EqualTo("szymon"));
                Assert.That(connection.SmartcardCertificate, Is.EqualTo("FIDO:ssh:blinky"));
                Assert.That(connection.SshAgentForwarding, Is.True);
                Assert.That(connection.Description, Is.EqualTo("Application server"));
                Assert.That(connection.PuttySession, Is.EqualTo(BlinkyAccessTreeSynchronizer.PuttySessionName));
            });
        }

        [Test]
        public void WithoutAPortTheProtocolDefaultIsUsed()
        {
            _synchronizer.Synchronize(_root, [DbConnection()]);

            Assert.That(Find(DbId).Port, Is.EqualTo(22));
        }

        [Test]
        public void AnUnknownProtocolFallsBackToSsh2RatherThanDroppingTheConnection()
        {
            BlinkyAccessConnection source = DbConnection();
            source.Protocol = "SOMETHING-NEW";

            _synchronizer.Synchronize(_root, [source]);

            Assert.That(Find(DbId).Protocol, Is.EqualTo(ProtocolType.SSH2));
        }

        [Test]
        public void ConnectionsWithoutAnIdAndRepeatedIdsAreSkipped()
        {
            BlinkyAccessConnection noId = DbConnection();
            noId.Id = "  ";

            BlinkyAccessConnection repeated = AppConnection();
            repeated.Name = "a second entry with the same id";

            BlinkyAccessSyncSummary summary = _synchronizer.Synchronize(_root, [AppConnection(), repeated, noId]);

            Assert.Multiple(() =>
            {
                Assert.That(summary.Added, Is.EqualTo(1));
                Assert.That(Find(AppId).Name, Is.EqualTo("szymon@app-01"));
            });
        }

        #endregion

        #region updating

        [Test]
        public void AConnectionWithTheSameIdIsUpdatedRatherThanAddedTwice()
        {
            _synchronizer.Synchronize(_root, [AppConnection()]);
            ConnectionInfo original = Find(AppId);

            BlinkyAccessConnection changed = AppConnection();
            changed.Name = "szymon@app-01 (renamed)";
            changed.Hostname = "app-01b.ad.digitalworkspace.pl";
            changed.Port = 22;
            changed.SmartcardCertificate = "";
            changed.SshAgentForwarding = false;

            BlinkyAccessSyncSummary summary = _synchronizer.Synchronize(_root, [changed]);

            ConnectionInfo updated = Find(AppId);
            Assert.Multiple(() =>
            {
                Assert.That(summary, Is.EqualTo(new BlinkyAccessSyncSummary(0, 1, 0)));
                Assert.That(updated, Is.SameAs(original), "the identity of the connection is kept");
                Assert.That(Branch().GetRecursiveChildList().Count(c => !c.IsContainer), Is.EqualTo(1));
                Assert.That(updated.Name, Is.EqualTo("szymon@app-01 (renamed)"));
                Assert.That(updated.Hostname, Is.EqualTo("app-01b.ad.digitalworkspace.pl"));
                Assert.That(updated.Port, Is.EqualTo(22));
                Assert.That(updated.SmartcardCertificate, Is.Empty);
                Assert.That(updated.SshAgentForwarding, Is.False);
            });
        }

        [Test]
        public void ARunThatChangesNothingCountsNothing()
        {
            _synchronizer.Synchronize(_root, [AppConnection(), DbConnection()]);

            BlinkyAccessSyncSummary summary = _synchronizer.Synchronize(_root, [AppConnection(), DbConnection()]);

            Assert.That(summary, Is.EqualTo(BlinkyAccessSyncSummary.Empty));
        }

        [Test]
        public void AConnectionFollowsItsFolderWhenThePortalMovesIt()
        {
            _synchronizer.Synchronize(_root, [AppConnection()]);

            BlinkyAccessConnection moved = AppConnection();
            moved.Folder = "Staging";

            BlinkyAccessSyncSummary summary = _synchronizer.Synchronize(_root, [moved]);

            Assert.Multiple(() =>
            {
                Assert.That(summary.Updated, Is.EqualTo(1));
                Assert.That(Find(AppId).Parent.Name, Is.EqualTo("Staging"));
                Assert.That(Branch().Children.OfType<ContainerInfo>().Select(c => c.Name),
                    Is.EqualTo(new[] { "Staging" }), "the folder it left is not kept around empty");
            });
        }

        [Test]
        public void TheUsersOwnRenamingOfTheBranchIsUndoneBecauseThePortalOwnsIt()
        {
            _synchronizer.Synchronize(_root, [AppConnection()]);
            string managedName = Branch().Name;
            Branch().Name = "my own name";

            _synchronizer.Synchronize(_root, [AppConnection()]);

            Assert.That(Branch().Name, Is.EqualTo(managedName));
        }

        #endregion

        #region removing

        [Test]
        public void AConnectionThePortalNoLongerReturnsIsRemoved()
        {
            _synchronizer.Synchronize(_root, [AppConnection(), DbConnection()]);

            BlinkyAccessSyncSummary summary = _synchronizer.Synchronize(_root, [DbConnection()]);

            Assert.Multiple(() =>
            {
                Assert.That(summary, Is.EqualTo(new BlinkyAccessSyncSummary(0, 0, 1)));
                Assert.That(Find(AppId), Is.Null);
                Assert.That(Find(DbId), Is.Not.Null);
            });
        }

        [Test]
        public void AnEmptyResponseEmptiesTheBranchButKeepsIt()
        {
            _synchronizer.Synchronize(_root, [AppConnection(), DbConnection()]);

            BlinkyAccessSyncSummary summary = _synchronizer.Synchronize(_root, []);

            Assert.Multiple(() =>
            {
                Assert.That(summary.Removed, Is.EqualTo(2));
                Assert.That(Branch(), Is.Not.Null);
                Assert.That(Branch().Children, Is.Empty);
            });
        }

        [Test]
        public void NothingOutsideTheBranchIsTouched()
        {
            ContainerInfo ownFolder = new() { Name = "My servers" };
            ConnectionInfo ownConnection = new() { Name = "my own connection", Hostname = "mine.example.com" };
            ownFolder.AddChild(ownConnection);
            _root.AddChild(ownFolder);

            // a connection of the user's that happens to carry a portal identifier must
            // not be adopted, moved or deleted by the sync
            ConnectionInfo lookalike = new(AppId) { Name = "not the portal's", Hostname = "lookalike.example.com" };
            ownFolder.AddChild(lookalike);

            _synchronizer.Synchronize(_root, [DbConnection()]);

            Assert.Multiple(() =>
            {
                Assert.That(_root.Children, Does.Contain(ownFolder));
                Assert.That(ownFolder.Children, Does.Contain(ownConnection));
                Assert.That(ownFolder.Children, Does.Contain(lookalike));
                Assert.That(lookalike.Name, Is.EqualTo("not the portal's"));
                Assert.That(lookalike.Hostname, Is.EqualTo("lookalike.example.com"));
            });
        }

        [Test]
        public void TheBranchAndWhatIsInsideItAreRecognisedAsManaged()
        {
            ConnectionInfo outside = new() { Name = "my own connection" };
            _root.AddChild(outside);

            _synchronizer.Synchronize(_root, [AppConnection()]);

            Assert.Multiple(() =>
            {
                Assert.That(BlinkyAccessTreeSynchronizer.IsManaged(Branch()), Is.True);
                Assert.That(BlinkyAccessTreeSynchronizer.IsManaged(Find(AppId)), Is.True);
                Assert.That(BlinkyAccessTreeSynchronizer.IsManaged(Find(AppId).Parent), Is.True);
                Assert.That(BlinkyAccessTreeSynchronizer.IsManaged(outside), Is.False);
                Assert.That(BlinkyAccessTreeSynchronizer.IsManaged(_root), Is.False);
            });
        }

        #endregion
    }
}
