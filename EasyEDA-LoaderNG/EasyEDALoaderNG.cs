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
            // Nettoyage : Suppression des logs de constructeur inutiles en prod
            noGUIMode = argClient.ProductInfo().SupportsUIFeature("NoGUI", false);
        }

        protected override IServerDocument NewDocumentInstance(
            string argKind,
            string argFileName) => null;

        protected override void InitializeCommands()
        {
            // On enregistre la commande silencieusement
            RegisterCommand("EasyEDARunNG", new CommandProc(Run));
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

                        // En cas de crash global du plugin, on affiche une popup explicite
                        MessageBox.Show(
                            ex.ToString(),
                            "EasyEDA Loader NG Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                });
        }


        // --- Lancement du Plugin
        private void Run(
            IServerDocumentView argContext,
            ref string argParameters)
        {
            if (noGUIMode)
                throw new InvalidOperationException("UI not available in NoGUI mode.");

            using var form = new LcscBrowserForm();

            // Nettoyage : On a supprimé le listener UrlChanged ici.
            // La Form gère déjà ses propres logs de navigation via la CheckBox Debug.
            // Plus besoin de faire remonter l'info au Module parent.

            form.ShowDialog();
        }
    }
}