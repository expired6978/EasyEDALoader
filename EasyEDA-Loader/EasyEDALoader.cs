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

        private static void PlaceComponent(string schLibraryPath, string partName)
        {
            var currentSheet = AltiumApi.GlobalVars.SCHServer.GetCurrentSchDocument();
            if (currentSheet == null)
                throw new InvalidOperationException("Must be in a schematic document before placing a component.");

            var newComponent = AltiumApi.GlobalVars.SCHServer.LoadComponentFromLibrary(partName, schLibraryPath);
            currentSheet.AddSchObject(newComponent);
            newComponent.MoveToXY(0, 0);
            newComponent.SetState_Orientation(TRotationBy90.eRotate0);
            currentSheet.GraphicallyInvalidate();
        }

        private void Run(
          IServerDocumentView argContext,
          ref string argParameters)
        {
            Dialog dialog = new Dialog();
            DialogResult result = dialog.ShowDialog();
            if (result != DialogResult.OK || dialog.SelectedComponents.Count == 0)
                return;

            var currentDoc = AltiumApi.GlobalVars.Client.GetCurrentView().GetOwnerDocument();
            if (currentDoc == null)
            {
                MessageBox.Show("Must be in a schematic document before running", "EasyEDA Loader Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }

            var ctx = new CancellationTokenSource();
            var api = new EasyedaApi();
            ImportLog.Reset();

            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string libraryPath = Path.Combine(documentsPath, "AltiumEE");
            Directory.CreateDirectory(libraryPath);
            string pcbLibraryPath = Path.Combine(libraryPath, "EasyEDA.pcblib");
            string schLibraryPath = Path.Combine(libraryPath, "EasyEDA.schlib");

            var pcbDocument = AltiumApi.GlobalVars.Client.OpenDocument("PcbLib", pcbLibraryPath);
            AltiumApi.GlobalVars.Client.ShowDocument(pcbDocument);
            var pcbLib = AltiumApi.GlobalVars.PCBServer.GetCurrentPCBLibrary();

            var schDocument = AltiumApi.GlobalVars.Client.OpenDocument("SchLib", schLibraryPath);
            AltiumApi.GlobalVars.Client.ShowDocument(schDocument);
            var schLib = EESCH.GetCurrentSchLibrary();

            // Process each selected component
            foreach (var selection in dialog.SelectedComponents)
            {
                try
                {
                    var root = selection.Root;
                    var owner_id = root.Component.Owner.Uuid;
                    var ee_footprint = root.Component.PackageDetail.Footprint;
                    // The top-level Symbol (root.Component.Symbol) has an empty shape list for
                    // multi-subpart components, but its Head.Parameters (Name/Pre) are still the
                    // correct ones for the component as a whole - a subpart's own Pre has a
                    // per-part suffix ("U?.1") that would be wrong here. Real per-part
                    // pins/shapes come from Subparts instead.
                    var ee_symbol = root.Component.Symbol;
                    bool isMultiPart = root.Component.Subparts != null && root.Component.Subparts.Count > 0;
                    string package = ee_footprint.Head.Parameters.Package;
                    EeFootprint3dModel model = selection.Include3dModel ? ee_footprint.GetModel() : null;

                    // Prefetch model if we can
                    Task<byte[]> modelTask = model != null ? Task.Run(() => api.LoadModelAsync(model.Uuid, ctx.Token)) : null;
                    Task<byte[]> rawModelTask = model != null ? Task.Run(() => api.LoadRawModelAsync(model.Uuid, ctx.Token)) : null;

                    // Get product info (use cached from search if available)
                    EasyedaApi.ProductInfo productInfo = selection.PartInfo.Info;

                    // Create PCB footprint if requested
                    if (selection.IncludeFootprint)
                    {
                        AltiumApi.GlobalVars.Client.ShowDocument(pcbDocument);
                        var libComp = pcbLib.GetComponentByName(package);
                        bool createdFootprint = false;
                        if (libComp == null)
                        {
                            libComp = EEPCB.CreateFootprintInLib(package, root.Component.PackageDetail.Title);
                            createdFootprint = libComp != null;
                        }

                        if (createdFootprint)
                        {
                            AltiumApi.GlobalVars.PCBServer.PreProcess();
                            var footprintContext = new EeFootprintContext
                            {
                                Box = ee_footprint.BoundingBox,
                                Layers = ee_footprint.Layers,
                                CancelToken = ctx.Token,
                                Exception = (Exception ex) =>
                                {
                                    ImportLog.Error($"footprint '{package}'", ex);
                                    return true;
                                },
                                ModelTask = modelTask,
                                RawModelTask = rawModelTask,
                            };
                            ee_footprint.AddToComponent(libComp, footprintContext);
                            AltiumApi.GlobalVars.PCBServer.PostProcess();
                            pcbDocument.DoFileSave("PcbLib");
                        }
                    }

                    // Create schematic symbol
                    string partName = ee_symbol.Head.Parameters.Name;
                    string description = productInfo?.Description ?? partName;

                    var existingComponent = schLib.GetState_SchComponentByLibRef(partName);
                    if (existingComponent == null)
                    {
                        var component = EESCH.CreateComponent(partName, description, ee_symbol.Head.Parameters.Pre);
                        if (schLib != null && component != null)
                        {
                            // Moved SetState_Current_SchComponent to before drawing, not after.
                            // SetState_CurrentSchComponentPartId (used per-subpart below to draw
                            // real multi-part components) needs the library's "current component"
                            // context to already be this component while per-part IDs are being
                            // set - otherwise every subpart's pins land in Part A regardless of
                            // the part ID set.
                            schLib.SetState_Current_SchComponent(component);
                            AltiumApi.GlobalVars.PCBServer.PreProcess();
                            if (isMultiPart)
                            {
                                SymbolDrawing.CreateMultiPartComponent(schLib, component, pcbLibraryPath, package, root.Component.Subparts);
                            }
                            else
                            {
                                SymbolDrawing.CreateComponent(schLib, component, pcbLibraryPath, package, ee_symbol);
                            }

                            if (productInfo?.Parameters != null)
                            {
                                foreach (var kvp in productInfo.Parameters)
                                {
                                    EESCH.AddParameter(component, kvp.Key, kvp.Value);
                                }
                            }

                            // Default Comment (shown as "*" until set). Comment isn't a plain
                            // parameter you add - it's a dedicated, always-present object
                            // retrieved via GetState_SchComment(), the same pattern already used
                            // above for the designator (GetState_SchDesignator()). Adding a
                            // separate parameter named "Comment" would create an unrelated extra
                            // parameter and leave the real comment field at its "*" default.
                            if (!string.IsNullOrEmpty(ee_symbol.Head.Parameters.ManufacturerPart))
                            {
                                var comment = component.GetState_SchComment();
                                comment.SetState_Text(ee_symbol.Head.Parameters.ManufacturerPart);
                                comment.SetState_ShowName(false);
                                comment.SetState_IsHidden(false);
                            }

                            AltiumApi.GlobalVars.PCBServer.PostProcess();
                            schLib.GraphicallyInvalidate();
                            schDocument.DoFileSave("SchLib");
                        }
                    }

                    // Place component in schematic if requested (only the last one)
                    if (dialog.PlaceInSchematic && selection == dialog.SelectedComponents[dialog.SelectedComponents.Count - 1])
                    {
                        // Return to the original document before placing
                        AltiumApi.GlobalVars.Client.ShowDocument(currentDoc);
                        PlaceComponent(schLibraryPath, partName);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to process component {selection.PartInfo.Name}: {ex.Message}", "EasyEDA Loader Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }
            }

            // Return to the original document we started in
            AltiumApi.GlobalVars.Client.ShowDocument(currentDoc);

            if (ImportLog.ErrorCount > 0)
            {
                MessageBox.Show(
                    $"{ImportLog.ErrorCount} primitive(s) failed to import. Details were written to:\n{ImportLog.LogPath}",
                    "EasyEDA Loader", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Close the library documents if requested
            if (dialog.CloseDocuments)
            {
                AltiumApi.GlobalVars.Client.CloseDocument(pcbDocument);
                AltiumApi.GlobalVars.Client.CloseDocument(schDocument);
            }
        }
    }
}
