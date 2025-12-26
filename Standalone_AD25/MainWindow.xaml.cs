using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HelixToolkit.Wpf;
using EasyEDA_Loader;

namespace Standalone_AD25
{
    public partial class MainWindow : System.Windows.Window
    {
        // --- Moteur EasyEDA ---
        protected EasyedaApi Api;
        protected ComponentInfo? Component;
        protected EeFootprint3dModel? Model;
        protected System.Windows.Media.Media3D.ModelVisual3D? RawModel;

        public System.Threading.CancellationTokenSource cts;

        // --- Helpers Visualisation ---
        public CanvasZoomPanHelper _footprintHelper;
        public CanvasZoomPanHelper _symbolHelper;

        // --- WebView ---
        private LCSCView? _view;

        public MainWindow()
        {
            InitializeComponent();

            cts = new System.Threading.CancellationTokenSource();
            Api = new EasyedaApi();

            _footprintHelper = new CanvasZoomPanHelper(FootprintCanvas);
            _symbolHelper = new CanvasZoomPanHelper(SymbolCanvas);

            Setup3DCamera();

            BtnExtractPdf.IsEnabled = false;
            BtnImportLib.IsEnabled = false;

            SearchBox.TextChanged += SearchBox_TextChanged;
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            cts.Cancel();
            cts.Dispose();
        }

        // -------------------------------------------------
        // CAMERA 3D
        // -------------------------------------------------

        private void Setup3DCamera()
        {
            var cam = ModelView.Camera as System.Windows.Media.Media3D.ProjectionCamera;
            if (cam != null)
            {
                cam.Position = new System.Windows.Media.Media3D.Point3D(0, 0, 30);
                cam.LookDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, -30);
                cam.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
                ModelView.Camera = cam;
                ModelView.ResetCamera();
            }
        }

        // -------------------------------------------------
        // WEBVIEW / URL
        // -------------------------------------------------

        private LCSCView EnsureView()
        {
            if (_view == null)
            {
                _view = new LCSCView();
                _view.UrlChanged += View_UrlChanged;
                ContentHost.Content = _view;
            }
            return _view;
        }

        private void View_UrlChanged(object? sender, string url)
        {
            var match = Regex.Match(url, @"C\d+");
            bool isProductPage = match.Success;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                new System.Action(() =>
                {
                    BtnExtractPdf.IsEnabled = isProductPage;

                    if (isProductPage)
                    {
                        SearchBox.Text = match.Value;
                        BtnImportLib.IsEnabled = true;
                    }
                }));
        }

        // -------------------------------------------------
        // SEARCH
        // -------------------------------------------------

        private void SearchBox_TextChanged(
            object sender,
            System.Windows.Controls.TextChangedEventArgs e)
        {
            string text = SearchBox.Text.Trim().ToUpperInvariant();
            bool seemsLikeLcscId = Regex.IsMatch(text, @"^C\d+[A-Z0-9\-]*$");
            BtnImportLib.IsEnabled = seemsLikeLcscId;
        }

        private async void BtnSearch_Click(
            object sender,
            System.Windows.RoutedEventArgs e)
        {
            try
            {
                string query = SearchBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(query))
                    return;

                var view = EnsureView();
                string url = BuildLCSCUrl(query);
                await view.NavigateToAsync(url);
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.ToString(),
                    "Recherche LCSC",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void SearchBox_KeyDown(
            object sender,
            System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                BtnSearch_Click(sender, e);
        }

        private static string BuildLCSCUrl(string query)
        {
            query = query.Trim().ToUpperInvariant();

            if (Regex.IsMatch(query, @"^C\d+[A-Z0-9\-]*$"))
                return $"https://www.lcsc.com/product-detail/{query}.html";

            string encoded = System.Uri.EscapeDataString(query);
            return $"https://www.lcsc.com/search?q={encoded}&s_z=n_{encoded}";
        }

        // -------------------------------------------------
        // FEATURE 1 : DATASHEET
        // -------------------------------------------------

        private async void BtnExtractPdf_Click(
            object sender,
            System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (_view == null)
                    return;

                BtnExtractPdf.IsEnabled = false;
                var win = CreateProgressWindow("Extraction Datasheet...");
                win.Show();

                try
                {
                    await _view.ExtractDatasheetAsync();
                }
                finally
                {
                    win.Close();
                    BtnExtractPdf.IsEnabled = true;
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.ToString(),
                    "Extraction PDF",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        // -------------------------------------------------
        // FEATURE 2 : IMPORT LIB
        // -------------------------------------------------

        private async void BtnImportLib_Click(
            object sender,
            System.Windows.RoutedEventArgs e)
        {
            try
            {
                string partId = SearchBox.Text.Trim().ToUpperInvariant();

                if (!partId.StartsWith("C"))
                {
                    System.Windows.MessageBox.Show(
                        "ID LCSC invalide (attendu: Cxxxx)",
                        "Erreur",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                PartId.Text = partId;

                if (RawModel != null)
                    ModelView.Children.Remove(RawModel);

                FootprintCanvas.Children.Clear();
                SymbolCanvas.Children.Clear();
                Thumbnail.Source = null;

                var win = CreateProgressWindow($"Importation {partId}...");
                win.Show();

                try
                {
                    await LoadComponentAsync(partId);
                }
                finally
                {
                    win.Close();
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.ToString(),
                    "Importation composant",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        // -------------------------------------------------
        // LOAD COMPONENT
        // -------------------------------------------------

        private async Task LoadComponentAsync(string partName)
        {
            try
            {
                var root = await Api.GetComponentJsonAsync(partName, cts.Token);
                if (root?.Component == null)
                    throw new System.Exception("Composant introuvable.");

                await Api.GetProductInfoAsync(partName, root.Component.Owner.Uuid);
                Component = root.Component;

                // -------- SYMBOL --------
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SymbolCanvas.Children.Clear();
                    SymbolDrawing.DrawComponent(SymbolCanvas, Component.Symbol.Shapes);
                    SymbolCanvas.UpdateLayout();

                    SymbolCanvas.Dispatcher.BeginInvoke(
                        new System.Action(() => _symbolHelper.FitToBoundingBox()),
                        System.Windows.Threading.DispatcherPriority.Loaded);
                });

                // -------- FOOTPRINT --------
                var eeFootprint = Component.PackageDetail.Footprint;
                Model = eeFootprint.GetModel();

                EeFootprintContext ctx = new()
                {
                    Box = eeFootprint.BoundingBox,
                    Layers = eeFootprint.Layers,
                    CancelToken = cts.Token
                };

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    FootprintCanvas.Children.Clear();
                    eeFootprint.DrawToCanvas(FootprintCanvas, ctx);
                    FootprintCanvas.UpdateLayout();

                    FootprintCanvas.Dispatcher.BeginInvoke(
                        new System.Action(() => _footprintHelper.FitToBoundingBox()),
                        System.Windows.Threading.DispatcherPriority.Loaded);
                });

                // -------- THUMBNAIL --------
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var img = await Api.LoadPngAsync(Component.Thumb, cts.Token);
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(
                            new System.Action(() => Thumbnail.Source = img));
                    }
                    catch { }
                });

                // -------- 3D MODEL --------
                if (Model != null)
                {
                    var rawModelData = await Api.LoadRawModelAsync(Model.Uuid, cts.Token);

                    using var stream = new MemoryStream(rawModelData);
                    var importer = new ObjReader();
                    var model3d = importer.Read(stream);

                    if (model3d != null)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (RawModel != null)
                                ModelView.Children.Remove(RawModel);

                            RawModel = new System.Windows.Media.Media3D.ModelVisual3D
                            {
                                Content = model3d
                            };

                            var tg = new System.Windows.Media.Media3D.Transform3DGroup();
                            tg.Children.Add(new System.Windows.Media.Media3D.RotateTransform3D(
                                new System.Windows.Media.Media3D.AxisAngleRotation3D(
                                    new System.Windows.Media.Media3D.Vector3D(1, 0, 0), Model.Rotation.X)));
                            tg.Children.Add(new System.Windows.Media.Media3D.RotateTransform3D(
                                new System.Windows.Media.Media3D.AxisAngleRotation3D(
                                    new System.Windows.Media.Media3D.Vector3D(0, 1, 0), Model.Rotation.Y)));
                            tg.Children.Add(new System.Windows.Media.Media3D.RotateTransform3D(
                                new System.Windows.Media.Media3D.AxisAngleRotation3D(
                                    new System.Windows.Media.Media3D.Vector3D(0, 0, 1), Model.Rotation.Z)));

                            RawModel.Transform = tg;
                            ModelView.Children.Add(RawModel);
                            ModelView.ZoomExtents();
                        });
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Erreur lors du chargement du composant:\n{ex.Message}",
                    "Erreur",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        // -------------------------------------------------
        // BACK
        // -------------------------------------------------

        private void BtnBack_Click(
            object sender,
            System.Windows.RoutedEventArgs e)
        {
            if (_view?.Browser?.CoreWebView2?.CanGoBack == true)
                _view.Browser.CoreWebView2.GoBack();
        }

        // -------------------------------------------------
        // PROGRESS WINDOW
        // -------------------------------------------------

        private System.Windows.Window CreateProgressWindow(string message)
        {
            var tb = new System.Windows.Controls.TextBlock
            {
                Text = message,
                Margin = new System.Windows.Thickness(20),
                TextAlignment = System.Windows.TextAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                FontSize = 14
            };

            return new System.Windows.Window
            {
                Title = "Processing",
                Content = tb,
                Width = 300,
                Height = 100,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                Owner = this,
                WindowStyle = System.Windows.WindowStyle.ToolWindow,
                ShowInTaskbar = false
            };
        }
    }
}
