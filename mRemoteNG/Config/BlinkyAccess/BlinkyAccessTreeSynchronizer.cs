using BlinkyAccess.Client;
using mRemoteNG.Connection;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Container;
using mRemoteNG.Resources.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace mRemoteNG.Config.BlinkyAccess
{
    /// <summary>What one synchronisation did to the managed branch.</summary>
    public sealed record BlinkyAccessSyncSummary(int Added, int Updated, int Removed)
    {
        public static BlinkyAccessSyncSummary Empty { get; } = new(0, 0, 0);

        public bool ChangedAnything => Added > 0 || Updated > 0 || Removed > 0;

        public override string ToString() => $"{Added} added, {Updated} updated, {Removed} removed";
    }

    /// <summary>
    /// Mirrors the connections of a Blinky Access portal into one branch of the regular
    /// connection tree, so they are saved, opened and themed like any other connection.
    ///
    /// The branch is owned by the portal: a connection it no longer returns is deleted,
    /// because the user has lost access to it, and the fields the API supplies are
    /// written back on every run. Nothing outside the branch is ever read or touched.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class BlinkyAccessTreeSynchronizer
    {
        /// <summary>
        /// Identity of the managed branch, fixed so the branch is still recognised after
        /// the user renames it or switches the application language.
        /// </summary>
        public const string BranchId = "6b1c0a7e-8f2d-4a3c-9c51-b11c4acce55a";

        /// <summary>The PuTTY session every mirrored connection is opened with.</summary>
        public const string PuttySessionName = "Default Settings";

        /// <summary>
        /// Brings the branch below <paramref name="root"/> in line with
        /// <paramref name="connections"/>, creating the branch when it is not there yet.
        /// </summary>
        public BlinkyAccessSyncSummary Synchronize(ContainerInfo root, IReadOnlyList<BlinkyAccessConnection> connections)
        {
            ArgumentNullException.ThrowIfNull(root);
            ArgumentNullException.ThrowIfNull(connections);

            ContainerInfo branch = FindBranch(root) ?? CreateBranch(root);

            // the branch is managed, so its own labelling is restored on every run
            branch.Name = Language.BlinkyAccessRootName;
            branch.Description = Language.BlinkyAccessBranchHint;

            Dictionary<string, ConnectionInfo> existing = ExistingConnections(branch);
            HashSet<string> seen = new(StringComparer.Ordinal);
            int added = 0;
            int updated = 0;

            // walked in the order the portal returned them, so a repeated id resolves
            // the same way every run; the display order is settled by SortBranch below
            foreach (BlinkyAccessConnection source in connections.Where(c => !string.IsNullOrWhiteSpace(c.Id)))
            {
                string id = source.Id.Trim();

                // a portal that repeats an id would otherwise have its later entry
                // silently overwrite the earlier one
                if (!seen.Add(id))
                    continue;

                ContainerInfo folder = FindOrCreateFolder(branch, source.Folder);

                if (existing.TryGetValue(id, out ConnectionInfo? connection))
                {
                    bool changed = Apply(source, connection);

                    if (!ReferenceEquals(connection.Parent, folder))
                    {
                        folder.AddChild(connection);
                        changed = true;
                    }

                    if (changed)
                        updated++;
                }
                else
                {
                    connection = new ConnectionInfo(id);
                    Apply(source, connection);
                    folder.AddChild(connection);
                    added++;
                }
            }

            int removed = RemoveConnectionsNotReturned(existing, seen);
            RemoveEmptyFolders(branch);
            RemoveStrandedBranches(root, branch);
            SortBranch(branch);

            return new BlinkyAccessSyncSummary(added, updated, removed);
        }

        /// <summary>
        /// The managed branch below <paramref name="root"/>, or null when there is none
        /// yet. Found by <see cref="BranchId"/> rather than by name.
        /// </summary>
        public static ContainerInfo? FindBranch(ContainerInfo root)
        {
            return root?.Children.OfType<ContainerInfo>().FirstOrDefault(IsBranch);
        }

        /// <summary>
        /// Whether a node is the managed branch or sits inside it. Used to keep the rest
        /// of the application from offering edits that the next sync would undo.
        /// </summary>
        public static bool IsManaged(ConnectionInfo node)
        {
            for (ConnectionInfo? current = node; current != null; current = current.Parent)
            {
                if (current is ContainerInfo container && IsBranch(container))
                    return true;
            }

            return false;
        }

        private static bool IsBranch(ContainerInfo container)
        {
            return string.Equals(container.ConstantID, BranchId, StringComparison.OrdinalIgnoreCase);
        }

        private static ContainerInfo CreateBranch(ContainerInfo root)
        {
            ContainerInfo branch = new(BranchId)
            {
                Name = Language.BlinkyAccessRootName,
                Description = Language.BlinkyAccessBranchHint,
                IsExpanded = true
            };

            root.AddChild(branch);
            return branch;
        }

        /// <summary>
        /// Every connection currently in the branch, by its identifier. Containers are
        /// left out: they carry no portal identity of their own.
        /// </summary>
        private static Dictionary<string, ConnectionInfo> ExistingConnections(ContainerInfo branch)
        {
            Dictionary<string, ConnectionInfo> connections = new(StringComparer.Ordinal);

            foreach (ConnectionInfo child in branch.GetRecursiveChildList().Where(c => !c.IsContainer))
                connections[child.ConstantID] = child;

            return connections;
        }

        private static ContainerInfo FindOrCreateFolder(ContainerInfo branch, string folderName)
        {
            string name = (folderName ?? "").Trim();

            if (name.Length == 0)
                return branch;

            ContainerInfo? folder = branch.Children
                .OfType<ContainerInfo>()
                .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.CurrentCultureIgnoreCase));

            if (folder != null)
                return folder;

            folder = new ContainerInfo { Name = name, IsExpanded = true };
            branch.AddChild(folder);
            return folder;
        }

        /// <summary>
        /// Writes the fields the API supplies onto a connection and reports whether any
        /// of them actually differed, so an unchanged run is not counted as an update.
        /// </summary>
        private static bool Apply(BlinkyAccessConnection source, ConnectionInfo target)
        {
            string name = target.Name;
            string hostname = target.Hostname;
            int port = target.Port;
            ProtocolType protocol = target.Protocol;
            string username = target.Username;
            string certificate = target.SmartcardCertificate;
            bool agentForwarding = target.SshAgentForwarding;
            string description = target.Description;
            string puttySession = target.PuttySession;

            target.Name = source.Name ?? "";
            target.Hostname = source.Hostname ?? "";
            target.Protocol = MapProtocol(source.Protocol);
            target.Username = source.Username ?? "";
            target.SmartcardCertificate = source.SmartcardCertificate ?? "";
            target.SshAgentForwarding = source.SshAgentForwarding;
            target.Description = source.Description ?? "";
            target.PuttySession = PuttySessionName;

            // the protocol has to be in place first: without a port the default of that
            // protocol is what the connection gets
            if (source.Port is > 0)
                target.Port = source.Port.Value;
            else
                target.SetDefaultPort();

            return name != target.Name
                || hostname != target.Hostname
                || port != target.Port
                || protocol != target.Protocol
                || username != target.Username
                || certificate != target.SmartcardCertificate
                || agentForwarding != target.SshAgentForwarding
                || description != target.Description
                || puttySession != target.PuttySession;
        }

        /// <summary>
        /// Reads the protocol the portal reports. An unknown name falls back to SSH2,
        /// which is what the portal hands out today; the connection is then still usable
        /// rather than being dropped.
        /// </summary>
        public static ProtocolType MapProtocol(string protocol)
        {
            return Enum.TryParse((protocol ?? "").Trim(), ignoreCase: true, out ProtocolType parsed)
                ? parsed
                : ProtocolType.SSH2;
        }

        private static int RemoveConnectionsNotReturned(Dictionary<string, ConnectionInfo> existing, HashSet<string> returnedIds)
        {
            int removed = 0;

            foreach (KeyValuePair<string, ConnectionInfo> entry in existing)
            {
                if (returnedIds.Contains(entry.Key))
                    continue;

                entry.Value.Parent?.RemoveChild(entry.Value);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// Drops folders left behind by connections that are gone. Only folders inside
        /// the branch are considered, and the branch itself is kept even when empty, so
        /// the user can see that the portal returned nothing.
        /// </summary>
        private static void RemoveEmptyFolders(ContainerInfo branch)
        {
            foreach (ContainerInfo folder in branch.Children.OfType<ContainerInfo>().ToArray())
            {
                if (!folder.HasChildren())
                    branch.RemoveChild(folder);
            }
        }

        /// <summary>
        /// Clears away copies of the branch left behind by versions that did not keep a
        /// container's identifier across a save and a load: the branch was not
        /// recognised on the next start and a second one was created beside it.
        ///
        /// Only a container that still looks exactly as this code left it is removed —
        /// the managed name and the managed hint, directly below the root — and nothing
        /// is lost by removing it, because everything it holds comes from the portal and
        /// is in the branch that stays.
        /// </summary>
        private static void RemoveStrandedBranches(ContainerInfo root, ContainerInfo branch)
        {
            foreach (ContainerInfo candidate in root.Children.OfType<ContainerInfo>().ToArray())
            {
                if (ReferenceEquals(candidate, branch))
                    continue;

                if (string.Equals(candidate.Name, Language.BlinkyAccessRootName, StringComparison.Ordinal) &&
                    string.Equals(candidate.Description, Language.BlinkyAccessBranchHint, StringComparison.Ordinal))
                {
                    root.RemoveChild(candidate);
                }
            }
        }

        private static void SortBranch(ContainerInfo branch)
        {
            branch.Sort();

            foreach (ContainerInfo folder in branch.Children.OfType<ContainerInfo>())
                folder.Sort();
        }
    }
}
