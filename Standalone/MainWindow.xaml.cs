using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

using EasyEDA_Loader;
using Microsoft.Win32;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using System.Windows.Threading;
using System.Collections.Generic;

namespace Standalone
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        protected Task<Root>? DocumentTask;
        protected Task<BitmapImage>? ThumbnailTask;
        protected Task<byte[]>? ModelTask;
        protected EasyedaApi Api;

        public ComponentInfo? Component;
        public EeFootprint3dModel? Model;
        public ModelVisual3D? RawModel;
        public CancellationTokenSource cts;

        public CanvasZoomPanHelper _footprintHelper;
        public CanvasZoomPanHelper _symbolHelper;

        public MainWindow()
        {
            cts = new CancellationTokenSource();
            Api = new EasyedaApi();
            InitializeComponent();

            _footprintHelper = new CanvasZoomPanHelper(FootprintCanvas);

            FootprintCanvasView.ScrollChanged += (s, e) =>
            {
                if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)
                    _footprintHelper.FitToBoundingBox();
            };

            _symbolHelper = new CanvasZoomPanHelper(SymbolCanvas);

            SymbolCanvasView.ScrollChanged += (s, e) =>
            {
                if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)
                    _symbolHelper.FitToBoundingBox();
            };

            var cam = ModelView.Camera as ProjectionCamera;
            if (cam != null)
            {
                cam.Position = new Point3D(0, 0, 30);       // Camera above the origin
                cam.LookDirection = new Vector3D(0, 0, -30); // Looking down at origin
                cam.UpDirection = new Vector3D(0, 1, 0);      // Y-axis as up

                ModelView.Camera = cam;

                // Optionally call ResetCamera() to update internals
                ModelView.ResetCamera();
            }
        }

        public static void SaveModelToFile(EeFootprint3dModel model, byte[] fileData)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Save File As",
                Filter = "SwSTEP 2.0|*.step|All Files|*.*",
                FileName = $"{model.Name}.step",
                DefaultExt = "step"
            };

            bool? result = saveFileDialog.ShowDialog();
            if (result == true)
            {
                string selectedPath = saveFileDialog.FileName;
                File.WriteAllBytes(selectedPath, fileData);
            }
        }
        public static void SaveRawModelToFile(EeFootprint3dModel model, byte[] fileData)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Save File As",
                Filter = "Obj|*.obj|All Files|*.*",
                FileName = $"{model.Name}.obj",
                DefaultExt = "obj"
            };

            bool? result = saveFileDialog.ShowDialog();
            if (result == true)
            {
                string selectedPath = saveFileDialog.FileName;
                File.WriteAllBytes(selectedPath, fileData);
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var api = new EasyedaApi();

            string partName = PartId.Text;
            if(RawModel != null)
                ModelView.Children.Remove(RawModel);

            FootprintCanvas.Children.Clear();

            var productTask = Task.Run(() => Api.GetProductInfoAsync(partName));
            productTask.Wait();

            DocumentTask = Task.Run(() => Api.GetComponentJsonAsync(partName, cts.Token));
            DocumentTask.Wait();
            Component = DocumentTask.Result.Component;

            SymbolDrawing.DrawComponent(SymbolCanvas, Component.Symbol.Shapes);
            SymbolCanvas.Dispatcher.InvokeAsync(() =>
            {
                _symbolHelper.FitToBoundingBox();
            }, DispatcherPriority.Loaded);

            var eeFootprint = Component.PackageDetail.Footprint;

            Model = eeFootprint.GetModel();

            ModelButton.IsEnabled = Model != null;
            ObjButton.IsEnabled = Model != null;

            try
            {
                ThumbnailTask = Task.Run(() => api.LoadPngAsync(Component.Thumb, cts.Token));
                ThumbnailTask.ContinueWith(t =>
                {
                    Thumbnail.Dispatcher.Invoke(() =>
                    {
                        if(t.Result != null)
                        {
                            Thumbnail.MaxWidth = t.Result.Width;
                            Thumbnail.MaxHeight = t.Result.Height;
                        }
                        Thumbnail.Source = t.Result;
                    });
                });
            }
            catch (Exception)
            {
                Thumbnail.Source = null;
                Thumbnail.MaxWidth = 0;
                Thumbnail.MaxHeight = 0;
            }

            EeFootprintContext ctx = new()
            {
                Box = eeFootprint.BoundingBox,
                Layers = eeFootprint.Layers,
                CancelToken = cts.Token,
                Exception = null,
            };

            byte[]? rawModelData = null;
            if(Model != null)
            {
                ctx.RawModelTask = Task.Run(() => Api.LoadRawModelAsync(Model.Uuid, cts.Token));
                var HeightTask = Task.Run(() => Model?.GetZOffsetFromOrigin(ctx));
                Task.WhenAll(ctx.RawModelTask, HeightTask).Wait();

                rawModelData = ctx.RawModelTask.Result;

                using var stream = new MemoryStream(rawModelData);
                var importer = new ObjReader();

                Model3D model = importer.Read(stream);
                if (model != null)
                {
                    RawModel = new ModelVisual3D { Content = model };
                    var transformGroup = new Transform3DGroup();
                    var rotationX = new AxisAngleRotation3D(new Vector3D(1, 0, 0), Model.Rotation.X);
                    var rotationY = new AxisAngleRotation3D(new Vector3D(0, 1, 0), Model.Rotation.Y);
                    var rotationZ = new AxisAngleRotation3D(new Vector3D(0, 0, 1), Model.Rotation.Z);
                    var rotateTransformX = new RotateTransform3D(rotationX);
                    var rotateTransformY = new RotateTransform3D(rotationY);
                    var rotateTransformZ = new RotateTransform3D(rotationZ);
                    transformGroup.Children.Add(rotateTransformX);
                    transformGroup.Children.Add(rotateTransformY);
                    transformGroup.Children.Add(rotateTransformZ);
                    RawModel.Transform = transformGroup;
                    ModelView.Children.Add(RawModel);
                }
            }

            eeFootprint.DrawToCanvas(FootprintCanvas, ctx);
            FootprintCanvas.Dispatcher.InvokeAsync(() =>
            {
                _footprintHelper.FitToBoundingBox();
            }, DispatcherPriority.Loaded);
        }

        private void ModelButton_Click(object sender, RoutedEventArgs e)
        {
            if(Model != null)
            {
                ModelTask = Task.Run(() => Api.LoadModelAsync(Model.Uuid, cts.Token));
                ModelTask.Wait();
                SaveModelToFile(Model, ModelTask.Result);
            }
        }
        private void ObjModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (Model != null)
            {
                ModelTask = Task.Run(() => Api.LoadRawModelAsync(Model.Uuid, cts.Token));
                ModelTask.Wait();
                SaveRawModelToFile(Model, ModelTask.Result);
            }
        }
    }
}
