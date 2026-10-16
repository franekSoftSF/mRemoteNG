using mRemoteNG.Resources.Language;
using mRemoteNG.Tools;
using System.Runtime.Versioning;

namespace mRemoteNG.Tree.Root
{
    /// <summary>
    /// Root of the mirrored BeyondTrust Password Safe accounts. Like the PuTTY sessions
    /// root this node is built at runtime and never written to the connections file.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class RootBeyondTrustNodeInfo : RootNodeInfo
    {
        private string _name;
        private string _panel;

        public RootBeyondTrustNodeInfo() : base(RootNodeType.BeyondTrust)
        {
            _name = Language.BeyondTrustRootName;
            _panel = Language.General;
        }

        #region Public Properties

        public override string Name
        {
            get => _name;
            set => _name = value;
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Display)),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Panel)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionPanel))]
        public override string Panel
        {
            get => _panel;
            set => _panel = value;
        }

        public override TreeNodeType GetTreeNodeType()
        {
            return TreeNodeType.BeyondTrustRoot;
        }

        #endregion

        /// <summary>
        /// True for this root and everything below it. Those nodes are rebuilt on every
        /// sync, so they must not be edited, renamed or deleted.
        /// </summary>
        public static bool IsBeyondTrustNode(Connection.ConnectionInfo node)
        {
            for (Connection.ConnectionInfo current = node; current != null; current = current.Parent)
            {
                if (current is RootBeyondTrustNodeInfo)
                    return true;
            }

            return false;
        }
    }
}
