using mRemoteNG.UI.Forms;
using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace mRemoteNG.UI.Controls
{
    /// <summary>
    /// Gives the smart card certificate property a browse button in the property grid,
    /// so the thumbprint is picked from the card instead of typed by hand.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class SmartcardCertificateEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            using SmartcardCertificateSelectionForm form = new(value as string ?? "");

            return form.ShowDialog() == DialogResult.OK
                ? form.SelectedIdentifier
                : value;
        }
    }
}
