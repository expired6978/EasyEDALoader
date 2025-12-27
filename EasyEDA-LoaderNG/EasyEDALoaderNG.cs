using DXP;
using PCB;
using SCH;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace EasyEDA_LoaderNG
{
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    public class EasyEDALoaderNGModule : ServerModule
    {
        private bool noGUIMode;

        // --- Culture fix (OBLIGATOIRE, on garde exactement la même)
        static EasyEDALoaderNGModule()
        {
            var culture = CultureInfo.InvariantCulture;

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        // --- Constructeur : NOM DU MODULE = CONTRAT AVEC ALTIUM
        public EasyEDALoaderNGModule(IClient argClient)
            : base(argClient, "EasyEDA-LoaderNG")
        {
            Helper.Log("Constructor called");

            noGUIMode = argClient.ProductInfo().SupportsUIFeature("NoGUI", false);
            Helper.Log($"noGUIMode = {noGUIMode}");
        }

        protected override IServerDocument NewDocumentInstance(
            string argKind,
            string argFileName) => null;

        protected override void InitializeCommands()
        {
            Helper.Log("InitializeCommands called");

            RegisterCommand("EasyEDARunNG", new CommandProc(Run));

            Helper.Log("Command EasyEDARunNG registered");
        }

        private void RegisterCommand(
            string argCommandId,
            CommandProc commandProc)
        {
            ((DXP.CommandLauncher)CommandLauncher).RegisterCommand(
                argCommandId,
                (IServerDocumentView view, ref string parameters) =>
                {
                    try
                    {
                        commandProc(view, ref parameters);
                    }
                    catch (Exception ex)
                    {
                        if (noGUIMode)
                            throw;

                        MessageBox.Show(
                            ex.ToString(),
                            "EasyEDA Loader NG Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                });
        }


        // --- PREUVE DE VIE
        private void Run(
            IServerDocumentView argContext,
            ref string argParameters)
        {
            if (noGUIMode)
                throw new InvalidOperationException("UI not available in NoGUI mode.");

            using var form = new LcscBrowserForm();

            // DEBUG / preuve de workflow
            form.UrlChanged += (_, url) =>
            {
                // Détection Cxxxx (comme Standalone)
                var match = Regex.Match(url, @"C\d+");
                if (match.Success)
                {
                    Helper.Log($"Detected SKU: {match.Value}");
                }
            };

            form.ShowDialog();
        }


    }
}
