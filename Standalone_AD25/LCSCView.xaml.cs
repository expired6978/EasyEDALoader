using System;
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

        // État d’extraction (ANTI double message)
        private bool _extractInProgress;

        // Nom du datasheet extrait depuis la page produit
        private string? _pendingDatasheetName;

        public event EventHandler<string>? UrlChanged;

        public LCSCView()
        {
            InitializeComponent();
            Loaded += async (_, __) => await EnsureBrowserReady();
        }

        public Microsoft.Web.WebView2.Wpf.WebView2 BrowserControl => Browser;

        // -------------------------------------------------
        // INIT WEBVIEW2
        // -------------------------------------------------

        private async Task EnsureBrowserReady()
        {
            if (Browser.CoreWebView2 != null)
                return;

            // Browser visible (page produit)
            await Browser.EnsureCoreWebView2Async();

            Browser.CoreWebView2.SourceChanged += (_, __) =>
                UrlChanged?.Invoke(this, Browser.Source?.ToString() ?? "");

            Browser.CoreWebView2.NewWindowRequested += (s, e) =>
            {
                e.Handled = true;
                Browser.CoreWebView2.Navigate(e.Uri);
            };

            // Browser caché (PDF)
            await HiddenBrowser.EnsureCoreWebView2Async();
            HiddenBrowser.CoreWebView2.Settings.IsWebMessageEnabled = true;

            if (!_webMessageHooked)
            {
                _webMessageHooked = true;
                HiddenBrowser.WebMessageReceived += HiddenBrowser_WebMessageReceived;
            }
        }

        // -------------------------------------------------
        // NAVIGATION
        // -------------------------------------------------

        public async Task NavigateToAsync(string url)
        {
            await EnsureBrowserReady();
            Browser.CoreWebView2.Navigate(url);
        }

        // -------------------------------------------------
        // EXTRACTION NOM DATASHEET (PAGE PRODUIT)
        // -------------------------------------------------

        private async Task<string?> ExtractDatasheetNameFromProductPageAsync()
        {
            await EnsureBrowserReady();

            string script = @"
(() => {
    const span = document.querySelector('a[href^=""/datasheet/""] span');
    if (!span) return null;
    return span.textContent.trim();
})();
";
            string json = await Browser.CoreWebView2.ExecuteScriptAsync(script);

            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return null;

            return JsonSerializer.Deserialize<string>(json);
        }

        // -------------------------------------------------
        // DATASHEET EXTRACTION (PDF)
        // -------------------------------------------------

        public async Task<bool> ExtractDatasheetAsync()
        {
            await EnsureBrowserReady();

            if (_extractInProgress)
                return false; // sécurité

            string currentUrl = Browser.Source?.ToString() ?? "";
            var match = Regex.Match(currentUrl, @"C\d+");

            if (!match.Success)
            {
                System.Windows.MessageBox.Show(
                    "Impossible de détecter le SKU (Cxxxx).",
                    "Erreur LCSC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            string sku = match.Value;

            // 🔹 Extraction du nom AVANT de quitter la page produit
            try
            {
                _pendingDatasheetName =
                    await ExtractDatasheetNameFromProductPageAsync();
            }
            catch
            {
                _pendingDatasheetName = null;
            }

            _extractTcs = new TaskCompletionSource<bool>();
            _extractInProgress = true;

            string viewerUrl = $"https://www.lcsc.com/datasheet/{sku}.pdf";
            HiddenBrowser.CoreWebView2.Navigate(viewerUrl);

            string script = @"
(async () => {
    const sleep = ms => new Promise(r => setTimeout(r, ms));

    let iframe = null;
    let attempts = 0;

    while (attempts < 50) {
        iframe = document.querySelector('iframe[src$="".pdf""]');
        if (iframe) break;
        await sleep(200);
        attempts++;
    }

    const targetUrl =
        iframe ? iframe.src :
        (window.location.href.endsWith('.pdf') ? window.location.href : null);

    if (!targetUrl) {
        window.chrome.webview.postMessage({
            type: 'PDF_ERROR',
            message: 'PDF introuvable'
        });
        return;
    }

    try {
        const res = await fetch(targetUrl);
        if (!res.ok) throw new Error('HTTP ' + res.status);

        const blob = await res.blob();
        const reader = new FileReader();

        reader.onloadend = () => {
            const base64 = reader.result.split(',')[1];
            window.chrome.webview.postMessage({
                type: 'PDF_CONTENT',
                payload: base64
            });
        };

        reader.readAsDataURL(blob);
    }
    catch (e) {
        window.chrome.webview.postMessage({
            type: 'PDF_ERROR',
            message: e.toString()
        });
    }
})();
";
            await Task.Delay(300);
            await HiddenBrowser.CoreWebView2.ExecuteScriptAsync(script);

            bool result = await _extractTcs.Task;

            // 🔒 NETTOYAGE SAFE (UNE SEULE FOIS)
            _extractTcs = null;
            _pendingDatasheetName = null;
            _extractInProgress = false;

            return result;
        }

        // -------------------------------------------------
        // WEB MESSAGE HANDLER (ANTI RACE CONDITION)
        // -------------------------------------------------

        private void HiddenBrowser_WebMessageReceived(
            object? sender,
            CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (!_extractInProgress || _extractTcs == null)
                return;

            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;

                string type = root.GetProperty("type").GetString() ?? "";

                if (type == "PDF_ERROR")
                {
                    _extractInProgress = false;

                    string msg = root.GetProperty("message").GetString()
                                 ?? "Erreur PDF";

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show(
                            msg,
                            "Erreur PDF",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });

                    _extractTcs.TrySetResult(false);
                }
                else if (type == "PDF_CONTENT")
                {
                    _extractInProgress = false;

                    byte[] bytes = Convert.FromBase64String(
                        root.GetProperty("payload").GetString() ?? "");

                    string filename =
                        !string.IsNullOrWhiteSpace(_pendingDatasheetName)
                            ? _pendingDatasheetName + ".pdf"
                            : "datasheet.pdf";

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dlg = new Microsoft.Win32.SaveFileDialog
                        {
                            Filter = "PDF (*.pdf)|*.pdf",
                            FileName = filename
                        };

                        if (dlg.ShowDialog() == true)
                        {
                            System.IO.File.WriteAllBytes(dlg.FileName, bytes);
                            _extractTcs.TrySetResult(true);
                        }
                        else
                        {
                            _extractTcs.TrySetResult(false);
                        }
                    });
                }
            }
            catch
            {
                _extractInProgress = false;
                _extractTcs?.TrySetResult(false);
            }
        }
    }
}
