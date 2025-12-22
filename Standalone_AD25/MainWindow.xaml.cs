using System;
using System.Windows;
using System.Text.RegularExpressions;

namespace Standalone_AD25
{
    public partial class MainWindow : Window
    {
        // Main embedded LCSC view
        private LCSCView? _view;

        public MainWindow()
        {
            InitializeComponent();
        }

        // =========================================================
        // Helpers
        // =========================================================
        private LCSCView EnsureView()
        {
            if (_view == null)
            {
                _view = new LCSCView();
                ContentHost.Content = _view;
            }

            return _view;
        }

        /// <summary>
        /// Builds the appropriate LCSC URL based on user input.
        /// - Direct SKU (Cxxxx) → product detail page
        /// - Otherwise → search page
        /// </summary>
        private static string BuildLCSCUrl(string query)
        {
            if (Regex.IsMatch(query, @"^C\d+[A-Z0-9\-]*$"))
            {
                return $"https://www.lcsc.com/product-detail/{query}.html";
            }

            string encoded = Uri.EscapeDataString(query);
            return $"https://www.lcsc.com/search?q={encoded}&s_z=n_{encoded}";
        }

        // =========================================================
        // UI Events
        // =========================================================
        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            string query = SearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
                return;

            query = query.ToUpperInvariant();

            var view = EnsureView();
            string url = BuildLCSCUrl(query);

            await view.NavigateToAsync(url);
        }

        private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnSearch_Click(sender, e);
            }
        }

        /// <summary>
        /// Extract datasheet from current product page
        /// </summary>
        private async void BtnExtractPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_view == null)
            {
                System.Windows.MessageBox.Show(
                    "No active LCSC page.",
                    "LCSC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Disable button during operation
            BtnExtractPdf.IsEnabled = false;

            // Create progress dialog
            var progressDialog = CreateProgressDialog(out var statusText);
            progressDialog.Show();

            bool success = false;

            try
            {
                statusText.Text = "Loading datasheet…";
                await Task.Delay(200); // allows UI refresh

                statusText.Text = "Extracting PDF…";
                success = await _view.ExtractDatasheetAsync();

                statusText.Text = "Saving file…";
                await Task.Delay(200);
            }
            finally
            {
                progressDialog.Close();
                BtnExtractPdf.IsEnabled = true;
            }

            if (!success)
            {
                System.Windows.MessageBox.Show(
                    "Datasheet extraction failed.",
                    "LCSC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }



        /// <summary>
        /// Manually create / show the WebView
        /// </summary>
        private void BtnBrowser_Click(object sender, RoutedEventArgs e)
        {
            EnsureView();
        }

        /// <summary>
        /// Navigate backward in WebView history
        /// </summary>
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_view?.Browser?.CoreWebView2?.CanGoBack == true)
            {
                _view.Browser.CoreWebView2.GoBack();
            }
        }

        private Window CreateProgressDialog(out System.Windows.Controls.TextBlock textBlock)
        {
            textBlock = new System.Windows.Controls.TextBlock
            {
                Text = "Working...",
                Margin = new Thickness(20),
                TextAlignment = TextAlignment.Center
            };

            var icon = new System.Windows.Controls.TextBlock
            {
                Text = "ℹ",
                FontSize = 24,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var panel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Children =
        {
            icon,
            textBlock
        }
            };

            return new Window
            {
                Title = "LCSC",
                Content = panel,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = this,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false
            };
        }

    }
}
