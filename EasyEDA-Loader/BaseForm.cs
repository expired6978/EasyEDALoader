using Altium.Controls;
using Altium.Controls.BaseForm;
using DevExpress.LookAndFeel;
using DevExpress.Skins;
using System;
using System.Windows.Forms;

namespace EasyEDA_Loader
{
    public class BaseForm : SynchronizationContextRestoreBaseXtraForm
    {
        static BaseForm()
        {
            SkinManager.EnableFormSkins();
            LookAndFeelHelper.ForceDefaultLookAndFeelChanged();
        }

        public BaseForm()
        {
            Activated += new EventHandler(BaseForm_Activated);
            Deactivate += new EventHandler(BaseForm_Deactivate);
            KeyPreview = true;
            KeyDown += new KeyEventHandler(BaseForm_KeyDown);
        }

        private void BaseForm_Deactivate(object sender, EventArgs e) => BackColor = Style.FormInactiveColor;

        private void BaseForm_Activated(object sender, EventArgs e) => BackColor = Style.FormActiveColor;

        public void ApplyStyle()
        {
            if (string.IsNullOrEmpty(Style.SkinName))
                Style.ApplyDefaultStyle((Control)this);
            else
                Style.ApplySkin((Control)this);
        }

        //protected virtual FormPainter CreateFormBorderPainter() => (FormPainter)new CustomFormPainter((Control)this, (ISkinProvider)this.LookAndFeel);

        private void BaseForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != System.Windows.Forms.Keys.F1 || !(sender is Control))
                return;
            //F1Helper.DisplayDialogHelp(this, sender as Control);
            e.Handled = true;
        }
    }
}
