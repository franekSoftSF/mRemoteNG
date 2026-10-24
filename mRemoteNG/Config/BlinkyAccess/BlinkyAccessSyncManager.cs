using BlinkyAccess.Client;
using mRemoteNG.App;
using mRemoteNG.Container;
using mRemoteNG.Messages;
using mRemoteNG.Resources.Language;
using mRemoteNG.Tree;
using mRemoteNG.Tree.Root;
using mRemoteNG.UI;
using mRemoteNG.UI.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;

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
        /// <param name="interactive">
        /// True when the user asked for this run from the menu, in which case the
        /// outcome is also put in front of them. A run nobody asked for, at startup,
        /// leaves it at the notification panel.
        /// </param>
        public void SyncAsync(bool interactive = false)
        {
            Thread thread = new(() => Sync(interactive)) { IsBackground = true, Name = "BlinkyAccessSync" };
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
        public BlinkyAccessSyncSummary Sync(bool interactive = false)
        {
            if (Interlocked.Exchange(ref _syncRunning, 1) == 1)
            {
                Report(MessageClass.InformationMsg, Language.BlinkyAccessAlreadyRunning, interactive, MessageBoxIcon.Information);
                return BlinkyAccessSyncSummary.Empty;
            }

            try
            {
                if (!BlinkyAccessSettings.IsConfigured)
                {
                    ReportNotConfigured(interactive);
                    return BlinkyAccessSyncSummary.Empty;
                }

                IReadOnlyList<BlinkyAccessConnection> connections;

                using (BlinkyAccessClient client = new(BlinkyAccessSettings.BuildClientOptions()))
                    connections = client.GetConnections();

                BlinkyAccessSyncSummary? summary = Apply(connections, interactive);

                if (summary != null && interactive)
                {
                    ShowDialog(string.Format(Language.BlinkyAccessSyncFinished,
                        summary.Added, summary.Updated, summary.Removed), MessageBoxIcon.Information);
                }

                return summary ?? BlinkyAccessSyncSummary.Empty;
            }
            catch (BlinkyAccessUnauthorizedException)
            {
                // deliberately not retried: only a new token can help
                Report(MessageClass.ErrorMsg, Language.BlinkyAccessTokenRejected, interactive, MessageBoxIcon.Error);
            }
            catch (BlinkyAccessThrottledException ex)
            {
                Report(MessageClass.WarningMsg, ex.RetryAfter.HasValue
                    ? string.Format(Language.BlinkyAccessRateLimitedRetryAfter, (int)ex.RetryAfter.Value.TotalSeconds)
                    : Language.BlinkyAccessRateLimited, interactive, MessageBoxIcon.Warning);
            }
            catch (BlinkyAccessUnsupportedFormatException ex)
            {
                Report(MessageClass.ErrorMsg, string.Format(Language.BlinkyAccessUnsupportedFormat, ex.ReportedFormat),
                    interactive, MessageBoxIcon.Error);
            }
            catch (BlinkyAccessConfigurationException)
            {
                ReportNotConfigured(interactive);
            }
            catch (Exception ex)
            {
                Report(MessageClass.ErrorMsg, $"{Language.BlinkyAccessSyncFailed}: {BlinkyAccessClient.Explain(ex)}",
                    interactive, MessageBoxIcon.Error);
            }
            finally
            {
                Interlocked.Exchange(ref _syncRunning, 0);
            }

            return BlinkyAccessSyncSummary.Empty;
        }

        /// <summary>
        /// Applies the connections to the managed branch. Null when there was no tree
        /// to apply them to, which is not the same as a run that changed nothing.
        /// </summary>
        private BlinkyAccessSyncSummary? Apply(IReadOnlyList<BlinkyAccessConnection> connections, bool interactive)
        {
            BlinkyAccessSyncSummary? summary = null;

            RunOnUiThread(() =>
            {
                ContainerInfo? root = ConnectionsRoot();

                if (root == null)
                {
                    Report(MessageClass.WarningMsg, Language.BlinkyAccessNoConnectionsLoaded, interactive, MessageBoxIcon.Warning);
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

        /// <summary>
        /// Puts an outcome where the user will find it: always in the notification
        /// panel, and in front of them as well when they asked for this run, since a
        /// menu item that answers only into a panel they may have hidden reads as a
        /// menu item that does nothing.
        /// </summary>
        private static void Report(MessageClass messageClass, string message, bool interactive, MessageBoxIcon icon)
        {
            Runtime.MessageCollector?.AddMessage(messageClass, message);

            if (interactive)
                ShowDialog(message, icon);
        }

        /// <summary>
        /// Nothing can be read without an address and a token, so an interactive run
        /// offers the page that asks for them instead of only naming it.
        /// </summary>
        private static void ReportNotConfigured(bool interactive)
        {
            Runtime.MessageCollector?.AddMessage(MessageClass.WarningMsg, Language.BlinkyAccessNotConfigured);

            if (!interactive)
                return;

            RunOnUiThread(() =>
            {
                DialogResult answer = MessageBox.Show(FrmMain.Default, Language.BlinkyAccessNotConfiguredOpenOptions,
                    Language.BlinkyAccessRootName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (answer != DialogResult.Yes)
                    return;

                AppWindows.Show(WindowType.Options);
                AppWindows.OptionsFormWindow?.SetActivatedPage(Language.BlinkyAccessRootName);
            });
        }

        private static void ShowDialog(string message, MessageBoxIcon icon)
        {
            RunOnUiThread(() => MessageBox.Show(FrmMain.Default, message, Language.BlinkyAccessRootName,
                MessageBoxButtons.OK, icon));
        }
    }
}
