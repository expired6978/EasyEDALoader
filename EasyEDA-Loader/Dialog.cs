using Altium.Controls;
using DevExpress.XtraEditors;
using System.Drawing;
using System.Windows.Forms;

namespace EasyEDA_Loader
{
    public class Dialog : BaseForm
    {
        private Panel clientPanel;

        private Label windowTextLabel;
        private TextEdit label2;
        private SimpleButton cancelButton;
        private SimpleButton okButton;

        public Dialog()
        {
            InitializeComponent();
            ApplyStyle();
            clientPanel.Paint += new PaintEventHandler(Style.DrawGrayBorder);
        }

        public string Component => label2.Text;

        private void InitializeComponent()
        {
            okButton = new SimpleButton();
            cancelButton = new SimpleButton();
            clientPanel = new Panel();
            label2 = new TextEdit();
            windowTextLabel = new Label();
            clientPanel.SuspendLayout();
            SuspendLayout();
            ((Control)okButton).Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            ((BaseButton)okButton).DialogResult = DialogResult.OK;
            ((Control)okButton).Location = new Point(332, 138);
            ((Control)okButton).Name = "okButton";
            ((Control)okButton).Size = new Size(75, 25);
            ((Control)okButton).TabIndex = 0;
            ((Control)okButton).Text = "OK";
            ((Control)cancelButton).Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            ((BaseButton)cancelButton).DialogResult = DialogResult.Cancel;
            ((Control)cancelButton).Location = new Point(413, 138);
            ((Control)cancelButton).Name = "cancelButton";
            ((Control)cancelButton).Size = new Size(75, 25);
            ((Control)cancelButton).TabIndex = 1;
            ((Control)cancelButton).Text = "Cancel";
            clientPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            clientPanel.Controls.Add((Control)label2);
            clientPanel.Controls.Add((Control)windowTextLabel);
            clientPanel.Location = new Point(0, 0);
            clientPanel.Name = "clientPanel";
            clientPanel.Size = new Size(488, 131);
            clientPanel.TabIndex = 1;
            label2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label2.Location = new Point(87, 44);
            label2.Name = "label2";
            label2.Size = new Size(390, 70);
            label2.TabIndex = 3;
            label2.Text = "C2040";
            windowTextLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            windowTextLabel.Location = new Point(77, 13);
            windowTextLabel.Name = "windowTextLabel";
            windowTextLabel.Size = new Size(400, 31);
            windowTextLabel.TabIndex = 1;
            windowTextLabel.Text = "Enter the part number to download";
            ((Form)this).AcceptButton = (IButtonControl)okButton;
            Appearance.Options.UseFont = true;
            ((ContainerControl)this).AutoScaleDimensions = new SizeF(96f, 96f);
            ((ContainerControl)this).AutoScaleMode = AutoScaleMode.Dpi;
            ((Form)this).CancelButton = (IButtonControl)cancelButton;
            ((Form)this).ClientSize = new Size(488, 164);
            ((Control)this).Controls.Add((Control)clientPanel);
            ((Control)this).Controls.Add((Control)okButton);
            ((Control)this).Controls.Add((Control)cancelButton);
            ((Control)this).Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point, (byte)0);
            ((Form)this).FormBorderStyle = FormBorderStyle.FixedDialog;
            ((Form)this).MaximizeBox = false;
            ((Form)this).MinimizeBox = false;
            ((Control)this).Name = nameof(Dialog);
            ((Form)this).ShowInTaskbar = false;
            ((Form)this).StartPosition = FormStartPosition.CenterScreen;
            ((Control)this).Text = "Confirmation";
            clientPanel.ResumeLayout(false);
            clientPanel.PerformLayout();
            ResumeLayout(false);
        }
    }
}
