using BlinkyAccess.Client;
using mRemoteNG.App;
using mRemoteNG.Container;
using mRemoteNG.Messages;
using mRemoteNG.Resources.Language;
using mRemoteNG.Tree;
using mRemoteNG.Tree.Root;
using mRemoteNG.UI.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;

namespace mRemoteNG.Config.BlinkyAccess
{
    /// <summary>
    /// Runs one synchronisation with the configured Blinky Access portal: reads the
    /// connections over the API on a background thread, then applies them to the
    /// managed branch of the connection tree on the UI thread and saves once.
    ///
    /// A failure leaves the tree exactly as it was. Only counts and status codes reach
    /// the log; the token, the request headers and the response body never do, and the
    /// response is never written to disk.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class BlinkyAccessSyncManager
    {
        public static BlinkyAccessSyncManager Instance { get; } = new();

        private readonly BlinkyAccessTreeSynchronizer _synchronizer = new();
        private int _syncRunning;

        private BlinkyAccessSyncManager()
        {
        }

        /// <summary>Runs <see cref="Sync"/> on a background thread.</summary>
        public void SyncAsync()
        {
            Thread thread = new(() => Sync()) { IsBackground = true, Name = "BlinkyAccessSync" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        /// <summary>
        /// Synchronises when the user asked for it at startup. Says nothing when the
        /// portal is not set up: an unconfigured feature must not greet the user with a
        /// message on every start.
        /// </summary>
        public void SyncOnStartupIfEnabled()
        {
            if (!BlinkyAccessSettings.SyncOnStartup || !BlinkyAccessSettings.IsConfigured)
                return;

            SyncAsync();
        }

        /// <summary>
        /// Reads the portal and brings the managed branch in line with it. Blocks, and
        /// may be called from any thread. Never throws.
        /// </summary>
        public BlinkyAccessSyncSummary Sync()
        {
            if (Interlocked.Exchange(ref _syncRunning, 1) == 1)
                return BlinkyAccessSyncSummary.Empty;

            try
            {
                if (!BlinkyAccessSettings.IsConfigured)
                {
                    Report(MessageClass.WarningMsg, Language.BlinkyAccessNotConfigured);
                    return BlinkyAccessSyncSummary.Empty;
                }

                IReadOnlyList<BlinkyAccessConnection> connections;

                using (BlinkyAccessClient client = new(BlinkyAccessSettings.BuildClientOptions()))
                    connections = client.GetConnections();

                return Apply(connections);
            }
            catch (BlinkyAccessUnauthorizedException)
            {
                // deliberately not retried: only a new token can help
                Report(MessageClass.ErrorMsg, Language.BlinkyAccessTokenRejected);
            }
            catch (BlinkyAccessThrottledException ex)
            {
                Report(MessageClass.WarningMsg, ex.RetryAfter.HasValue
                    ? string.Format(Language.BlinkyAccessRateLimitedRetryAfter, (int)ex.RetryAfter.Value.TotalSeconds)
                    : Language.BlinkyAccessRateLimited);
            }
            catch (BlinkyAccessUnsupportedFormatException ex)
            {
                Report(MessageClass.ErrorMsg, string.Format(Language.BlinkyAccessUnsupportedFormat, ex.ReportedFormat));
            }
            catch (BlinkyAccessConfigurationException)
            {
                Report(MessageClass.WarningMsg, Language.BlinkyAccessNotConfigured);
            }
            catch (Exception ex)
            {
                Report(MessageClass.ErrorMsg, $"{Language.BlinkyAccessSyncFailed}: {BlinkyAccessClient.Explain(ex)}");
            }
            finally
            {
                Interlocked.Exchange(ref _syncRunning, 0);
            }

            return BlinkyAccessSyncSummary.Empty;
        }

        private BlinkyAccessSyncSummary Apply(IReadOnlyList<BlinkyAccessConnection> connections)
        {
            BlinkyAccessSyncSummary summary = BlinkyAccessSyncSummary.Empty;

            RunOnUiThread(() =>
            {
                ContainerInfo? root = ConnectionsRoot();

                if (root == null)
                {
                    Report(MessageClass.WarningMsg, Language.BlinkyAccessNoConnectionsLoaded);
                    return;
                }

                // one save for the whole branch instead of one per changed property,
                // and asked for explicitly so the branch is kept even for users who
                // have saving after every edit turned off
                using (Runtime.ConnectionsService.BatchedSavingContext())
                {
                    summary = _synchronizer.Synchronize(root, connections);

                    if (summary.ChangedAnything)
                        Runtime.ConnectionsService.SaveConnectionsAsync();
                }

                // the counts are the only thing worth keeping: no names, no token, no body
                Runtime.MessageCollector?.AddMessage(MessageClass.InformationMsg,
                    string.Format(Language.BlinkyAccessSyncResult, summary.Added, summary.Updated, summary.Removed), true);
            });

            return summary;
        }

        /// <summary>
        /// The root of the user's own connections, which is the only place the managed
        /// branch is ever put. Null when no connections file is loaded yet.
        /// </summary>
        private static ContainerInfo? ConnectionsRoot()
        {
            ConnectionTreeModel model = Runtime.ConnectionsService.ConnectionTreeModel;

            return model?.RootNodes
                .OfType<RootNodeInfo>()
                .FirstOrDefault(node => node.Type == RootNodeType.Connection);
        }

        /// <summary>
        /// The tree is bound to the connection tree view, so every change to it has to
        /// happen on the UI thread. Falls through to a direct call when there is no
        /// window to marshal to, which is the case in tests and during shutdown.
        /// </summary>
        private static void RunOnUiThread(Action action)
        {
            FrmMain mainForm = FrmMain.Default;

            if (mainForm is { IsDisposed: false, IsHandleCreated: true } && mainForm.InvokeRequired)
                mainForm.Invoke(action);
            else
                action();
        }

        private static void Report(MessageClass messageClass, string message)
        {
            Runtime.MessageCollector?.AddMessage(messageClass, message);
        }
    }
}
