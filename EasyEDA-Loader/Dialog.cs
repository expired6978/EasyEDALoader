using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace EasyEDA_Loader
{
    /// <summary>
    /// WinForms wrapper for the WPF DialogWindow to maintain compatibility with existing code
    /// </summary>
    public class Dialog
    {
        private DialogWindow wpfDialog;

        public List<ComponentSelection> SelectedComponents => wpfDialog?.SelectedComponents;
        public bool CloseDocuments => wpfDialog?.CloseDocuments ?? false;
        public bool PlaceInSchematic => wpfDialog?.PlaceInSchematic ?? true;

        public Dialog()
        {
            wpfDialog = new DialogWindow();
        }

        public DialogResult ShowDialog()
        {
            // Show the WPF dialog and convert the result to WinForms DialogResult
            bool? result = wpfDialog.ShowDialog();
            
            if (result == true)
                return DialogResult.OK;
            else if (result == false)
                return DialogResult.Cancel;
            else
                return DialogResult.None;
        }
    }

    public class ComponentSelection
    {
        public EasyedaApi.PartInfo PartInfo { get; set; }
        public Root Root { get; set; }
        public bool Include3dModel { get; set; }
        public bool IncludeFootprint { get; set; }
    }
}
