using ExternalConnectors.BT;
using mRemoteNG.App;
using mRemoteNG.Connection;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Container;
using mRemoteNG.Messages;
using mRemoteNG.Resources.Language;
using mRemoteNG.Tree.Root;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Timers;

namespace mRemoteNG.Config.BeyondTrust
{
    /// <summary>
    /// Mirrors the requestable managed accounts of BeyondTrust Password Safe into a
    /// dedicated tree root. The accounts are grouped by managed system and carry the
    /// BeyondTrust credential provider, so the password itself is only released when a
    /// connection is actually opened.
    ///
    /// Nothing is fetched until <see cref="Sync"/> is called, which keeps the API login
    /// prompt out of application startup.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class BeyondTrustSyncManager
    {
        public static BeyondTrustSyncManager Instance { get; } = new();

        private const double RefreshIntervalMinutes = 15;

        private readonly System.Timers.Timer _refreshTimer;
        private int _syncRunning;

        public RootBeyondTrustNodeInfo RootNode { get; } = new();

        /// <summary>True once accounts have been read at least once.</summary>
        public bool HasSynced { get; private set; }

        public event NotifyCollectionChangedEventHandler AccountsCollectionChanged;

        private BeyondTrustSyncManager()
        {
            _refreshTimer = new System.Timers.Timer(TimeSpan.FromMinutes(RefreshIntervalMinutes).TotalMilliseconds)
            {
                AutoReset = true
            };
            _refreshTimer.Elapsed += OnRefreshTimerElapsed;
        }

        /// <summary>
        /// Reads every requestable account and rebuilds the tree below the root node.
        /// Blocks; the API login dialog is shown on the calling thread.
        /// </summary>
        public void Sync()
        {
            if (Interlocked.Exchange(ref _syncRunning, 1) == 1)
                return;

            try
            {
                IReadOnlyList<BTManagedAccount> accounts = BeyondTrustInterface.ListManagedAccounts();
                BuildTree(accounts);
                HasSynced = true;
                _refreshTimer.Enabled = true;

                Runtime.MessageCollector?.AddMessage(MessageClass.InformationMsg,
                    $"{Language.BeyondTrustRootName}: {accounts.Count} accounts", true);

                AccountsCollectionChanged?.Invoke(this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage(Language.BeyondTrustSyncFailed, ex);
            }
            finally
            {
                Interlocked.Exchange(ref _syncRunning, 0);
            }
        }

        /// <summary>Runs <see cref="Sync"/> on a background thread.</summary>
        public void SyncAsync()
        {
            Thread thread = new(Sync) { IsBackground = true, Name = "BeyondTrustSync" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        /// <summary>Stops the periodic refresh, checks released passwords back in and signs out.</summary>
        public void Shutdown()
        {
            _refreshTimer.Enabled = false;

            if (!HasSynced)
                return;

            try
            {
                BeyondTrustInterface.SignOut();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionStackTrace("Could not sign out of BeyondTrust Password Safe", ex);
            }
        }

        private void OnRefreshTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // the session is already established at this point, so no dialog can appear
            Sync();
        }

        private void BuildTree(IReadOnlyList<BTManagedAccount> accounts)
        {
            ConnectionInfo[] oldChildren = [.. RootNode.Children];
            foreach (ConnectionInfo child in oldChildren)
                RootNode.RemoveChild(child);

            foreach (IGrouping<string, BTManagedAccount> systemGroup in accounts
                         .GroupBy(a => string.IsNullOrEmpty(a.SystemName) ? a.Host : a.SystemName)
                         .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                ContainerInfo systemContainer = new()
                {
                    Name = systemGroup.Key
                };

                foreach (BTManagedAccount account in systemGroup.OrderBy(a => a.AccountName, StringComparer.CurrentCultureIgnoreCase))
                    systemContainer.AddChild(CreateConnection(account));

                RootNode.AddChild(systemContainer);
            }
        }

        private ConnectionInfo CreateConnection(BTManagedAccount account)
        {
            ConnectionInfo connection = new()
            {
                Name = account.AccountName,
                Description = account.AccountDescription ?? "",
                Hostname = account.Host,
                Protocol = MapProtocol(account),
                ExternalCredentialProvider = ExternalCredentialProvider.BeyondTrustPasswordSafe,
                UserViaAPI = account.SecretId,
                Domain = account.DomainName ?? ""
            };

            if (account.Port is > 0)
                connection.Port = account.Port.Value;

            return connection;
        }

        /// <summary>
        /// Derives the protocol from the Password Safe platform, falling back to the port
        /// of the managed system.
        /// </summary>
        private static ProtocolType MapProtocol(BTManagedAccount account)
        {
            string platform = account.PlatformName ?? "";

            if (platform.Contains("windows", StringComparison.OrdinalIgnoreCase))
                return ProtocolType.RDP;

            string[] sshPlatforms = ["linux", "unix", "aix", "solaris", "hp-ux", "hpux", "macos", "mac os", "esx", "cisco", "juniper", "f5", "palo alto", "fortinet"];
            if (sshPlatforms.Any(p => platform.Contains(p, StringComparison.OrdinalIgnoreCase)))
                return ProtocolType.SSH2;

            return account.Port switch
            {
                3389 => ProtocolType.RDP,
                22 => ProtocolType.SSH2,
                _ => ProtocolType.SSH2
            };
        }
    }
}
