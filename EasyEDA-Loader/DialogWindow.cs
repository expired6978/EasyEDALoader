using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DevExpress.Xpf.Grid;
using DevExpress.LookAndFeel;
using DevExpress.Skins;
using Microsoft.Win32;

namespace EasyEDA_Loader
{
    public partial class DialogWindow : Window
    {
        private EasyedaApi Api;
        private CancellationTokenSource cts;
        private CancellationTokenSource previewCts;
        private ObservableCollection<PartInfoViewModel> searchResults;
        private CanvasZoomPanHelper _footprintHelper;
        private CanvasZoomPanHelper _symbolHelper;
        private ComponentInfo _currentComponent;
        private EeFootprint3dModel _currentModel;
        private Root _currentRoot;

        public List<ComponentSelection> SelectedComponents { get; private set; }
        public bool CloseDocuments => closeDocumentsCheckBox?.IsChecked == true;
        public bool PlaceInSchematic => placeInSchematicCheckBox?.IsChecked == true;

        public DialogWindow()
        {
            InitializeComponent();
            
            Api = new EasyedaApi();
            cts = new CancellationTokenSource();
            previewCts = new CancellationTokenSource();
            searchResults = new ObservableCollection<PartInfoViewModel>();
            SelectedComponents = new List<ComponentSelection>();
            
            resultsGrid.ItemsSource = searchResults;
            
            resultsGrid.SelectionChanged += ResultsGrid_SelectionChanged;

            _footprintHelper = new CanvasZoomPanHelper(footprintCanvas);
            footprintCanvasView.ScrollChanged += (s, e) =>
            {
                if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)
                    _footprintHelper.FitToBoundingBox();
            };

            _symbolHelper = new CanvasZoomPanHelper(symbolCanvas);
            symbolCanvasView.ScrollChanged += (s, e) =>
            {
                if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)
                    _symbolHelper.FitToBoundingBox();
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply Altium theme colors after window is loaded
            ApplyAltiumTheme();
        }

        private void ApplyAltiumTheme()
        {
            try
            {
                // Get Altium's skin colors
                var lookAndFeel = new UserLookAndFeel(this);
                var skin = CommonSkins.GetSkin(lookAndFeel);
                
                if (skin != null)
                {
                    // Apply background color to main grid
                    var windowColor = skin.Colors.GetColor("Window");
                    var mainGrid = (System.Windows.Controls.Grid)this.Content;
                    mainGrid.Background = new SolidColorBrush(Color.FromArgb(
                        windowColor.A, windowColor.R, windowColor.G, windowColor.B));
                    
                    // Apply text color to all TextBlocks
                    var textColor = skin.Colors.GetColor("WindowText");
                    var textBrush = new SolidColorBrush(Color.FromArgb(
                        textColor.A, textColor.R, textColor.G, textColor.B));
                    
                    // Set foreground for the window (will cascade to children)
                    this.Resources[SystemColors.WindowTextBrushKey] = textBrush;
                    
                    // Apply to TextBlocks directly
                    ApplyColorToTextBlocks(mainGrid, textBrush);
                }
            }
            catch
            {
                // If theme detection fails, use dark colors as fallback
                var mainGrid = (System.Windows.Controls.Grid)this.Content;
                mainGrid.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                
                var textBrush = new SolidColorBrush(Color.FromRgb(241, 241, 241));
                ApplyColorToTextBlocks(mainGrid, textBrush);
            }
        }

        private void ApplyColorToTextBlocks(System.Windows.DependencyObject parent, SolidColorBrush brush)
        {
            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is System.Windows.Controls.TextBlock textBlock)
                {
                    textBlock.Foreground = brush;
                }
                else if (child is System.Windows.Controls.ContentControl contentControl)
                {
                    contentControl.Foreground = brush;
                }
                
                // Recursively apply to children
                ApplyColorToTextBlocks(child, brush);
            }
        }

        private void ResultsGrid_SelectionChanged(object sender, GridSelectionChangedEventArgs e)
        {
            UpdateAddButtonState();
        }

        private async void ResultsGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
        {
            previewCts?.Cancel();
            previewCts?.Dispose();
            previewCts = new CancellationTokenSource();

            if (e.NewItem is PartInfoViewModel partViewModel)
            {
                await LoadPreviewAsync(partViewModel, previewCts.Token);
            }
            else
            {
                ClearPreview();
            }
        }

        private async Task LoadPreviewAsync(PartInfoViewModel partViewModel, CancellationToken cancellationToken)
        {
            try
            {
                thumbnailImage.Source = null;
                symbolCanvas.Children.Clear();
                footprintCanvas.Children.Clear();
                _currentComponent = null;
                _currentModel = null;
                _currentRoot = null;
                saveModelButton.IsEnabled = false;

                var root = await Task.Run(() => Api.GetComponentJsonAsync(partViewModel.PartInfo.Part, cancellationToken));

                if (cancellationToken.IsCancellationRequested)
                    return;

                if (root?.Component != null)
                {
                    _currentComponent = root.Component;
                    _currentRoot = root;

                if (_currentComponent.Symbol?.Shapes != null)
                {
                    SymbolDrawing.DrawComponent(symbolCanvas, _currentComponent.Symbol.Shapes);
                    _ = symbolCanvas.Dispatcher.InvokeAsync(() =>
                    {
                        _symbolHelper.FitToBoundingBox();
                    }, DispatcherPriority.Loaded);
                }

                    if (_currentComponent.PackageDetail?.Footprint != null)
                    {
                        var eeFootprint = _currentComponent.PackageDetail.Footprint;
                        _currentModel = eeFootprint.GetModel();

                        saveModelButton.IsEnabled = _currentModel != null;

                        EeFootprintContext ctx = new EeFootprintContext
                        {
                            Box = eeFootprint.BoundingBox,
                            Layers = eeFootprint.Layers,
                            CancelToken = cancellationToken,
                            Exception = null,
                        };

                        eeFootprint.DrawToCanvas(footprintCanvas, ctx);
                        
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        _ = footprintCanvas.Dispatcher.InvokeAsync(() =>
                        {
                            _footprintHelper.FitToBoundingBox();
                        }, DispatcherPriority.Loaded);
                    }

                    if (!string.IsNullOrEmpty(_currentComponent.Thumb))
                    {
                        try
                        {
                            var thumbnail = await Task.Run(() => Api.LoadPngAsync(_currentComponent.Thumb, cancellationToken));
                            
                            if (cancellationToken.IsCancellationRequested)
                                return;

                            if (thumbnail != null)
                            {
                                thumbnailImage.Source = thumbnail;
                                thumbnailImage.MaxWidth = thumbnail.Width;
                                thumbnailImage.MaxHeight = thumbnail.Height;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }

        private void ClearPreview()
        {
            thumbnailImage.Source = null;
            symbolCanvas.Children.Clear();
            footprintCanvas.Children.Clear();
            _currentComponent = null;
            _currentModel = null;
            _currentRoot = null;
            saveModelButton.IsEnabled = false;
        }

        public void UpdateAddButtonState()
        {
            addToLibraryButton.IsEnabled = searchResults.Any(p => p.AddToLibrary);
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(searchTextBox.Text))
            {
                MessageBox.Show("Please enter a part number to search.", "Search Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            searchButton.IsEnabled = false;
            addToLibraryButton.IsEnabled = false;
            searchResults.Clear();

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                
                // Run the API call on a background thread
                var searchText = searchTextBox.Text;
                var results = await Task.Run(() => Api.SearchProductInfoAsync(searchText));

                // Add results on the UI thread
                if (results != null && results.Count > 0)
                {
                    foreach (var part in results)
                    {
                        searchResults.Add(new PartInfoViewModel(part, this));
                    }
                }
                else
                {
                    MessageBox.Show("No results found.", "Search", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                searchButton.IsEnabled = true;
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SearchButton_Click(sender, e);
            }
        }

        private async void AddToLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedParts = searchResults.Where(p => p.AddToLibrary).ToList();
            
            if (selectedParts.Count == 0)
            {
                MessageBox.Show("Please select at least one component to add.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            addToLibraryButton.IsEnabled = false;
            cancelButton.IsEnabled = false;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                SelectedComponents.Clear();

                foreach (var partViewModel in selectedParts)
                {
                    var partInfo = partViewModel.PartInfo;
                    
                    // Fetch component data
                    var root = await Task.Run(() => Api.GetComponentJsonAsync(partInfo.Part, cts.Token));

                    if (root?.Component != null)
                    {
                        var component = root.Component;
                        var has3dModel = component.PackageDetail?.Footprint?.GetModel() != null;
                        var hasFootprint = component.PackageDetail?.Footprint != null;

                        // Update the view model with actual data
                        partViewModel.HasFootprint = hasFootprint;
                        partViewModel.Has3d = has3dModel;

                        SelectedComponents.Add(new ComponentSelection
                        {
                            PartInfo = partInfo,
                            Root = root,
                            Include3dModel = has3dModel,
                            IncludeFootprint = hasFootprint
                        });
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load component data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                addToLibraryButton.IsEnabled = true;
                cancelButton.IsEnabled = true;
                Mouse.OverrideCursor = null;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void SaveModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentModel == null)
                return;

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Save Model File As",
                Filter = "STEP Files (*.step)|*.step|All Files (*.*)|*.*",
                FileName = $"{_currentModel.Name}.step",
                DefaultExt = "step"
            };

            bool? result = saveFileDialog.ShowDialog();
            if (result == true)
            {
                saveModelButton.IsEnabled = false;
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    var modelData = await Task.Run(() => Api.LoadModelAsync(_currentModel.Uuid, cts.Token));

                    if (modelData != null && modelData.Length > 0)
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, modelData);
                        MessageBox.Show($"Model saved successfully to {saveFileDialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to download model data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save model: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                    saveModelButton.IsEnabled = _currentModel != null;
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            cts?.Cancel();
            previewCts?.Cancel();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            cts?.Dispose();
            previewCts?.Dispose();
            base.OnClosed(e);
        }
    }

    public class PartInfoViewModel : INotifyPropertyChanged
    {
        private bool _addToLibrary;
        private bool _hasFootprint;
        private bool _has3d;
        private readonly DialogWindow _parentWindow;

        public EasyedaApi.PartInfo PartInfo { get; }

        public bool AddToLibrary
        {
            get => _addToLibrary;
            set
            {
                if (_addToLibrary != value)
                {
                    _addToLibrary = value;
                    OnPropertyChanged(nameof(AddToLibrary));
                    _parentWindow?.UpdateAddButtonState();
                }
            }
        }

        public bool HasFootprint
        {
            get => _hasFootprint;
            set
            {
                if (_hasFootprint != value)
                {
                    _hasFootprint = value;
                    OnPropertyChanged(nameof(HasFootprint));
                }
            }
        }

        public bool Has3d
        {
            get => _has3d;
            set
            {
                if (_has3d != value)
                {
                    _has3d = value;
                    OnPropertyChanged(nameof(Has3d));
                }
            }
        }

        public string Name => PartInfo.Name ?? PartInfo.Part;
        public string Description => PartInfo.Description ?? "";

        public PartInfoViewModel(EasyedaApi.PartInfo partInfo, DialogWindow parentWindow)
        {
            PartInfo = partInfo;
            _parentWindow = parentWindow;
            _hasFootprint = partInfo.HasFootprint;
            _has3d = partInfo.Has3d;
            _addToLibrary = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
