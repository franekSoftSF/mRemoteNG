using mRemoteNG.App;
using mRemoteNG.Connection;
using mRemoteNG.Container;
using mRemoteNG.Messages;
using mRemoteNG.Tree;
using mRemoteNG.Tree.Root;
using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace mRemoteNG.UI.Forms
{
    /// <summary>
    /// Shows <see cref="SmartcardConnectionForm"/> and puts the resulting connection
    /// into the tree, next to whatever is selected.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class SmartcardConnectionCreator
    {
        public static void CreateInteractive()
        {
            try
            {
                using SmartcardConnectionForm form = new();

                if (form.ShowDialog() != DialogResult.OK || form.Connection == null)
                    return;

                ContainerInfo parent = GetTargetContainer();
                if (parent == null)
                {
                    Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg,
                        "Cannot add the connection because no connection file is loaded.");
                    return;
                }

                form.Connection.SetParent(parent);
                Runtime.ConnectionsService.SaveConnectionsAsync();

                AppWindows.TreeForm.ConnectionTree.SelectObject(form.Connection, true);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace("Could not create a smart card connection", ex);
            }
        }

        /// <summary>
        /// The selected folder, the folder of the selected connection, or the root of
        /// the real connection tree. Never the mirrored vault or PuTTY roots, which are
        /// rebuilt and would throw the new connection away.
        /// </summary>
        private static ContainerInfo GetTargetContainer()
        {
            ConnectionTreeModel model = Runtime.ConnectionsService.ConnectionTreeModel;
            if (model == null)
                return null;

            ConnectionInfo selected = AppWindows.TreeForm.ConnectionTree.SelectedNode;

            if (selected != null && !RootBeyondTrustNodeInfo.IsBeyondTrustNode(selected) &&
                !IsPuttyNode(selected))
            {
                if (selected is ContainerInfo container)
                    return container;

                if (selected.Parent != null)
                    return selected.Parent;
            }

            return model.RootNodes.OfType<RootNodeInfo>()
                       .FirstOrDefault(node => node is not RootPuttySessionsNodeInfo and not RootBeyondTrustNodeInfo);
        }

        private static bool IsPuttyNode(ConnectionInfo node)
        {
            for (ConnectionInfo current = node; current != null; current = current.Parent)
            {
                if (current is RootPuttySessionsNodeInfo || current is PuttySessionInfo)
                    return true;
            }

            return false;
        }
    }
}
