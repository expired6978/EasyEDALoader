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
        private TaskCompletionSource<bool>? _pocTcs;
        public LCSCView()
        {
            InitializeComponent();

            Loaded += async (_, __) =>
            {
                await EnsureBrowserReady();
            };
        }

        // ===== POC synchronization =====
        private TaskCompletionSource<bool>? _pocCompletionSource;

        // Ensures WebMessageReceived is hooked only once
        private bool _webMessageHooked = false;

        /// <summary>
        /// Receives messages from injected JavaScript (PDF extraction POC)
        /// </summary>
        private void HiddenBrowser_WebMessageReceived(
            object? sender,
            CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;

                string? type = root.TryGetProperty("type", out var t)
                    ? t.GetString()
                    : null;

                // --- Error coming from JavaScript ---
                if (type == "PDF_ERROR")
                {
                    string message = root.TryGetProperty("message", out var m)
                        ? m.GetString() ?? "Unknown error"
                        : "Unknown error";

                    System.Windows.MessageBox.Show(
                        "PDF extraction failed:\n" + message,
                        "LCSC",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    _pocTcs?.TrySetResult(false);
                    return;
                }

                // --- Ignore unrelated messages ---
                if (type != "PDF_CONTENT")
                    return;

                // --- Extract PDF binary ---
                var bytes = root.GetProperty("payload")
                                .EnumerateArray()
                                .Select(x => (byte)x.GetInt32())
                                .ToArray();

                if (bytes.Length < 1024)
                {
                    System.Windows.MessageBox.Show(
                        "Received PDF is too small and likely invalid.",
                        "LCSC",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    _pocTcs?.TrySetResult(false);
                    return;
                }

                // --- Extract PDF URL (used for filename) ---
                string pdfUrl = root.TryGetProperty("url", out var u)
                    ? u.GetString() ?? ""
                    : "";

                string defaultFileName = BuildPdfFileName(pdfUrl);

                // --- Ask user where to save ---
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Save LCSC datasheet",
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = defaultFileName,
                    InitialDirectory = Environment.GetFolderPath(
                        Environment.SpecialFolder.Desktop),
                    OverwritePrompt = false
                };

                if (dlg.ShowDialog() != true)
                {
                    _pocTcs?.TrySetResult(false);
                    return;
                }

                // --- Write file (overwrite-safe) ---
                try
                {
                    if (System.IO.File.Exists(dlg.FileName))
                    {
                        System.IO.File.Delete(dlg.FileName);
                    }

                    System.IO.File.WriteAllBytes(dlg.FileName, bytes);

                    System.Windows.MessageBox.Show(
                        $"PDF saved successfully:\n{dlg.FileName}\nSize: {bytes.Length:N0} bytes",
                        "LCSC",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    _pocTcs?.TrySetResult(true);
                }
                catch (Exception ioEx)
                {
                    System.Windows.MessageBox.Show(
                        "Failed to write PDF file:\n" + ioEx.Message,
                        "LCSC",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    _pocTcs?.TrySetResult(false);
                }


                _pocTcs?.TrySetResult(true);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Unexpected error while saving PDF:\n" + ex.Message,
                    "LCSC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                _pocTcs?.TrySetResult(false);
            }
        }


        /// <summary>
        /// Initializes visible and hidden WebView2 instances (once)
        /// </summary>
        private async Task EnsureBrowserReady()
        {
            if (Browser.CoreWebView2 != null)
                return;

            // === Visible WebView ===
            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            Browser.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
            Browser.CoreWebView2.Settings.IsWebMessageEnabled = true;

            // Prevent window.open()
            Browser.CoreWebView2.NewWindowRequested += (sender, args) =>
            {
                args.Handled = true;
                Browser.CoreWebView2.Navigate(args.Uri);
            };

            // === Hidden WebView (PDF processing) ===
            await HiddenBrowser.EnsureCoreWebView2Async();
            HiddenBrowser.CoreWebView2.Settings.IsWebMessageEnabled = true;
            HiddenBrowser.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;

            if (!_webMessageHooked)
            {
                _webMessageHooked = true;
                HiddenBrowser.WebMessageReceived += HiddenBrowser_WebMessageReceived;
            }
        }

        /// <summary>
        /// Handles navigation errors
        /// </summary>
        private void OnNavigationCompleted(
            object? sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || (e.HttpStatusCode >= 400 && e.HttpStatusCode <= 599))
            {
                System.Windows.MessageBox.Show(
                    $"Navigation failed (HTTP {e.HttpStatusCode})",
                    "WebView2",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// External navigation entry point
        /// </summary>
        public async Task NavigateToAsync(string url)
        {
            await EnsureBrowserReady();
            Browser.CoreWebView2.Navigate(url);
        }

        /// <summary>
        /// Detects the "The part does not exist" LCSC page
        /// </summary>
        public async Task<bool> IsNonExistingAsync()
        {
            await EnsureBrowserReady();

            // Structural check
            string jsCheck = "(() => document.querySelector('.none-product-layout') !== null)();";
            string result = (await Browser.ExecuteScriptAsync(jsCheck)).Trim('"');

            if (result == "true")
                return true;

            // Text fallback
            string body = (await Browser.ExecuteScriptAsync("document.body.innerText")).Trim('"');
            return body.Contains("The part does not exist", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// POC: Extract PDF from LCSC datasheet viewer and save to Desktop
        /// </summary>
        public async Task<bool> Poc_ExtractPdfToDesktopAsync()
        {
            await EnsureBrowserReady();

            string currentUrl = Browser.Source?.ToString() ?? "";
            var match = Regex.Match(currentUrl, @"C\d+");

            if (!match.Success)
            {
                System.Windows.MessageBox.Show(
                    "Unable to find LCSC part number (Cxxxx) in URL.",
                    "LCSC POC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            string sku = match.Value;
            string viewerUrl = $"https://www.lcsc.com/datasheet/{sku}.pdf";

            HiddenBrowser.CoreWebView2.Navigate(viewerUrl);

            _pocCompletionSource = new TaskCompletionSource<bool>();

            // Simple delay (POC only)
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


            bool success = await _pocCompletionSource.Task;
            _pocCompletionSource = null;
            return success;
        }

        /// <summary>
        /// Intercepts WebView2 downloads and shows a SaveFileDialog
        /// </summary>
        private void CoreWebView2_DownloadStarting(
            object? sender,
            CoreWebView2DownloadStartingEventArgs e)
        {
            e.Handled = true;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = System.IO.Path.GetFileName(e.ResultFilePath)
            };

            if (dialog.ShowDialog() == true)
            {
                e.ResultFilePath = dialog.FileName;
            }
            else
            {
                e.Cancel = true;
            }
        }

        private static string BuildPdfFileName(string pdfUrl)
        {
            try
            {
                string fileName = System.IO.Path.GetFileName(new Uri(pdfUrl).AbsolutePath);

                // Safety fallback
                if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    return "datasheet.pdf";

                // Windows-safe filename
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                    fileName = fileName.Replace(c, '_');

                return fileName;
            }
            catch
            {
                return "datasheet.pdf";
            }
        }

    }
}
