using System;
using System.Windows;

namespace Standalone_AD25
{
    public partial class MainWindow : Window
    {
        // Main embedded LCSC view
        private LCSCView? view;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Builds the appropriate LCSC URL based on user input.
        /// - Direct SKU (Cxxxx) → product detail page
        /// - Otherwise → search page
        /// </summary>
        private string BuildLCSCUrl(string query)
        {
            // Direct SKU → product detail
            if (System.Text.RegularExpressions.Regex.IsMatch(query, @"^C\d+[A-Z0-9\-]*$"))
            {
                return $"https://www.lcsc.com/product-detail/{query}.html";
            }

            // Fallback → search engine
            string encoded = Uri.EscapeDataString(query);
            return $"https://www.lcsc.com/search?q={encoded}&s_z=n_{encoded}";
        }

        /// <summary>
        /// Search button handler
        /// </summary>
        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            string query = SearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
                return;

            // Lazily create the LCSC view
            if (view == null)
            {
                view = new LCSCView();
                ContentHost.Content = view;
            }

            query = query.ToUpperInvariant();
            string url = BuildLCSCUrl(query);

            await view.NavigateToAsync(url);
        }

        /// <summary>
        /// Allows pressing ENTER in the search box
        /// </summary>
        private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnSearch_Click(sender, e);
            }
        }

        /*
        /// <summary>
        /// Legacy datasheet download workflow (disabled)
        /// </summary>
        private async void BtnExtractPdf_Click(object sender, RoutedEventArgs e)
        {
            if (view == null)
            {
                view = new LCSCView();
                ContentHost.Content = view;
            }

            if (await view.IsNonExistingAsync())
            {
                System.Windows.MessageBox.Show("The part does not exist.");
                return;
            }

            await view.DownloadDatasheetFromProductPageAsync();
        }
        */

        /// <summary>
        /// POC: Extract PDF directly from datasheet viewer
        /// </summary>
        private async void BtnExtractPdf_Click(object sender, RoutedEventArgs e)
        {
            if (view == null)
            {
                System.Windows.MessageBox.Show(
                    "No active LCSC view.",
                    "LCSC POC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            bool success = await view.Poc_ExtractPdfToDesktopAsync();
            if (!success)
            {
                System.Windows.MessageBox.Show(
                    "PDF extraction failed.",
                    "LCSC POC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Manual button to create / display the WebView
        /// </summary>
        private void BtnBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (view == null)
            {
                view = new LCSCView();
                ContentHost.Content = view;
            }
        }

        /// <summary>
        /// Navigate backward in WebView history
        /// </summary>
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (view?.Browser?.CoreWebView2?.CanGoBack == true)
            {
                view.Browser.CoreWebView2.GoBack();
            }
        }
    }
}
