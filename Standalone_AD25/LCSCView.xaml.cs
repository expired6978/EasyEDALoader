using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Standalone_AD25
{
    public partial class LCSCView : System.Windows.Controls.UserControl
    {
        private TaskCompletionSource<bool>? _extractTcs;
        private bool _webMessageHooked;

        public LCSCView()
        {
            InitializeComponent();

            Loaded += async (_, __) =>
            {
                await EnsureBrowserReady();
            };
        }

        // =========================================================
        // WebView initialization
        // =========================================================
        private async Task EnsureBrowserReady()
        {
            if (Browser.CoreWebView2 != null)
                return;

            // Visible WebView (navigation)
            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            Browser.CoreWebView2.Settings.IsWebMessageEnabled = true;

            Browser.CoreWebView2.NewWindowRequested += (s, e) =>
            {
                e.Handled = true;
                Browser.CoreWebView2.Navigate(e.Uri);
            };

            // Hidden WebView (PDF extraction)
            await HiddenBrowser.EnsureCoreWebView2Async();
            HiddenBrowser.CoreWebView2.Settings.IsWebMessageEnabled = true;

            if (!_webMessageHooked)
            {
                _webMessageHooked = true;
                HiddenBrowser.WebMessageReceived += HiddenBrowser_WebMessageReceived;
            }
        }

        private void OnNavigationCompleted(
            object? sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || (e.HttpStatusCode >= 400 && e.HttpStatusCode <= 599))
            {
                System.Windows.MessageBox.Show(
                    $"Navigation failed (HTTP {e.HttpStatusCode})",
                    "LCSC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        public async Task NavigateToAsync(string url)
        {
            await EnsureBrowserReady();
            Browser.CoreWebView2.Navigate(url);
        }

        // =========================================================
        // Datasheet extraction (FINAL workflow)
        // =========================================================
        public async Task<bool> ExtractDatasheetAsync()
        {
            await EnsureBrowserReady();

            string currentUrl = Browser.Source?.ToString() ?? "";
            var match = Regex.Match(currentUrl, @"C\d+");

            if (!match.Success)
            {
                System.Windows.MessageBox.Show(
                    "Unable to detect LCSC part number (Cxxxx).",
                    "LCSC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            string sku = match.Value;
            string viewerUrl = $"https://www.lcsc.com/datasheet/{sku}.pdf";

            _extractTcs = new TaskCompletionSource<bool>();

            HiddenBrowser.CoreWebView2.Navigate(viewerUrl);

            // Simple delay (viewer load)
            await Task.Delay(3000);

            await HiddenBrowser.CoreWebView2.ExecuteScriptAsync(@"
(async () => {
  try {
    const iframe = document.querySelector('iframe[src$="".pdf""]');
    if (!iframe) {
      window.chrome.webview.postMessage({
        type: 'PDF_ERROR',
        message: 'PDF iframe not found'
      });
      return;
    }

    const pdfUrl = iframe.src;
    const res = await fetch(pdfUrl);
    if (!res.ok) {
      window.chrome.webview.postMessage({
        type: 'PDF_ERROR',
        message: 'PDF fetch failed: ' + res.status
      });
      return;
    }

    const buf = await res.arrayBuffer();
    const arr = Array.from(new Uint8Array(buf));

    window.chrome.webview.postMessage({
      type: 'PDF_CONTENT',
      payload: arr,
      url: pdfUrl
    });
  } catch (e) {
    window.chrome.webview.postMessage({
      type: 'PDF_ERROR',
      message: e.toString()
    });
  }
})();
");

            bool ok = await _extractTcs.Task;
            _extractTcs = null;
            return ok;
        }

        // =========================================================
        // WebMessage receiver
        // =========================================================
        private void HiddenBrowser_WebMessageReceived(
            object? sender,
            CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (_extractTcs == null)
                return;

            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;

                string type = root.GetProperty("type").GetString() ?? "";

                if (type == "PDF_ERROR")
                {
                    System.Windows.MessageBox.Show(
                        root.GetProperty("message").GetString(),
                        "LCSC",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    _extractTcs.TrySetResult(false);
                    return;
                }

                if (type != "PDF_CONTENT")
                    return;

                byte[] bytes = root.GetProperty("payload")
                                   .EnumerateArray()
                                   .Select(x => (byte)x.GetInt32())
                                   .ToArray();

                string pdfUrl = root.GetProperty("url").GetString() ?? "";
                string defaultName = BuildPdfFileName(pdfUrl);

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Save LCSC datasheet",
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = defaultName,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (dlg.ShowDialog() != true)
                {
                    _extractTcs.TrySetResult(false);
                    return;
                }

                System.IO.File.WriteAllBytes(dlg.FileName, bytes);

                _extractTcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.Message,
                    "LCSC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                _extractTcs.TrySetResult(false);
            }
        }

        private static string BuildPdfFileName(string pdfUrl)
        {
            try
            {
                string name = System.IO.Path.GetFileName(new Uri(pdfUrl).AbsolutePath);
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                    name = name.Replace(c, '_');
                return string.IsNullOrWhiteSpace(name) ? "datasheet.pdf" : name;
            }
            catch
            {
                return "datasheet.pdf";
            }
        }
    }
}
