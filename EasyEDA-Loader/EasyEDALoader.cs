using DXP;
using PCB;
using SCH;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyEDA_Loader
{
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    public class EasyEDALoaderModule : ServerModule
    {
        private bool noGUIMode;

        public EasyEDALoaderModule(IClient argClient)
          : base(argClient, "EasyEDA-Loader")
        {
            noGUIMode = argClient.ProductInfo().SupportsUIFeature("NoGUI", false);
        }

        protected override IServerDocument NewDocumentInstance(string argKind, string argFileName) => (IServerDocument)null;

        protected override void InitializeCommands() => RegisterCommand("EasyEDARun", new CommandProc(Run));

        private void RegisterCommand(string argCommandId, CommandProc commandProc) => ((DXP.CommandLauncher)CommandLauncher).RegisterCommand(argCommandId, (CommandProc)((IServerDocumentView view, ref string parameters) =>
        {
            try
            {
                commandProc(view, ref parameters);
            }
            catch (Exception ex)
            {
                if (noGUIMode)
                {
                    throw;
                }
                else
                {
                    int num = (int)MessageBox.Show(ex.Message, "EasyEDA Loader Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }
            }
        }));

        private void Run(
          IServerDocumentView argContext,
          ref string argParameters)
        {
            Dialog dialog = new Dialog();
            DialogResult result = dialog.ShowDialog();
            if (result != DialogResult.OK)
                return;

            var currentDoc = AltiumApi.GlobalVars.Client.GetCurrentView().GetOwnerDocument();
            if (currentDoc == null)
            {
                MessageBox.Show("Must be in a schematic document before running", "EasyEDA Loader Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }

            var ctx = new CancellationTokenSource(); // Not used yet, maybe if we make this a window and not a Dialog?
            var api = new EasyedaApi();

            Task<Root> root = null;
            try
            {
                root = Task.Run(() => api.GetComponentJsonAsync(dialog.Component, ctx.Token));
                root.Wait();
            }
            catch (Exception)
            {
                MessageBox.Show($"Failed to retrieve component info for {dialog.Component}", "EasyEDA Loader Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }

            var owner_id = root.Result.Component.Owner.Uuid;
            var ee_footprint = root.Result.Component.PackageDetail.Footprint;
            var ee_symbol = root.Result.Component.Symbol;
            string package = ee_footprint.Head.Parameters.Package;
            EeFootprint3dModel model = ee_footprint.GetModel();

            // Prefetch model if we can
            Task<byte[]> modelTask = model != null ? Task.Run(() => api.LoadModelAsync(model.Uuid, ctx.Token)) : null;
            Task<byte[]> rawModelTask = model != null ? Task.Run(() => api.LoadRawModelAsync(model.Uuid, ctx.Token)) : null;

            Task<EasyedaApi.ProductInfo> productInfo = Task.Run(() => api.GetProductInfoAsync(dialog.Component, owner_id));

            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            string libraryPath = Path.Combine(documentsPath, "AltiumEE");
            Directory.CreateDirectory(libraryPath);
            string pcbLibraryPath = Path.Combine(libraryPath, "EasyEDA.pcblib");
            string schLibraryPath = Path.Combine(libraryPath, "EasyEDA.schlib");

            var pcbDocument = AltiumApi.GlobalVars.Client.OpenDocument("PcbLib", pcbLibraryPath);
            AltiumApi.GlobalVars.Client.ShowDocument(pcbDocument);

            var pcbLib = AltiumApi.GlobalVars.PCBServer.GetCurrentPCBLibrary();
            var libComp = pcbLib.GetComponentByName(package);
            if (libComp != null)
            {
                // Return to current document and close PcbLib
                AltiumApi.GlobalVars.Client.ShowDocument(currentDoc);
                AltiumApi.GlobalVars.Client.CloseDocument(pcbDocument);
                MessageBox.Show($"Footprint {package} already exists in Library", "EasyEDA Loader Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }

            libComp = EEPCB.CreateFootprintInLib(package, root.Result.Component.PackageDetail.Title);
            if (libComp != null)
            {
                AltiumApi.GlobalVars.PCBServer.PreProcess();
                var footprintContext = new EeFootprintContext
                {
                    Box = ee_footprint.BoundingBox,
                    Layers = ee_footprint.Layers,
                    CancelToken = ctx.Token,
                    Exception = (Exception ex) =>
                    {
                        // Log problems here?
                        return true;
                    },
                    ModelTask = modelTask,
                    RawModelTask = rawModelTask,
                };
                ee_footprint.AddToComponent(libComp, footprintContext);
                AltiumApi.GlobalVars.PCBServer.PostProcess();
                pcbDocument.DoFileSave("PcbLib");
            }

            var schDocument = AltiumApi.GlobalVars.Client.OpenDocument("SchLib", schLibraryPath);
            AltiumApi.GlobalVars.Client.ShowDocument(schDocument);

            // Probably finished by now but we need it now
            productInfo.Wait();

            string partName = ee_symbol.Head.Parameters.Name;
            string description = productInfo.Result?.Description ?? partName;

            (var schLib, var component) = EESCH.CreateComponentInLib(partName, description, ee_symbol.Head.Parameters.Pre);
            if (schLib != null && component != null)
            {
                AltiumApi.GlobalVars.PCBServer.PreProcess();
                SymbolDrawing.CreateComponent(schLib, component, pcbLibraryPath, package, ee_symbol);

                foreach (var kvp in productInfo.Result?.Parameters)
                {
                    EESCH.AddParameter(component, kvp.Key, kvp.Value);
                }

                AltiumApi.GlobalVars.PCBServer.PostProcess();
                schLib.SetState_Current_SchComponent(component);
                schLib.GraphicallyInvalidate();
                schDocument.DoFileSave("SchLib");
            }

            // Return to the original document we started in
            AltiumApi.GlobalVars.Client.ShowDocument(currentDoc);

            // Close the library documents
            AltiumApi.GlobalVars.Client.CloseDocument(pcbDocument);
            AltiumApi.GlobalVars.Client.CloseDocument(schDocument);

            var newComponent = AltiumApi.GlobalVars.SCHServer.LoadComponentFromLibrary(partName, schLibraryPath);
            var currentSheet = AltiumApi.GlobalVars.SCHServer.GetCurrentSchDocument();
            currentSheet.AddSchObject(newComponent);
            newComponent.MoveToXY(0, 0);
            newComponent.SetState_Orientation(TRotationBy90.eRotate0);
            currentSheet.GraphicallyInvalidate();

            // Add the component to the current schematic
            //string param = $"LibraryName={schLibraryPath};ComponentName={partName}";
            //DXP.Utils.RunCommand("SCH:PlaceComponent", param, AltiumApi.GlobalVars.Client.GetCurrentView());
            //IProcessLauncher client = AltiumApi.GlobalVars.Client as IProcessLauncher;
            //client.SendMessage("SCH:PlaceComponent", ref param, (object)AltiumApi.GlobalVars.Client.GetCurrentView());

        }
    }
}
