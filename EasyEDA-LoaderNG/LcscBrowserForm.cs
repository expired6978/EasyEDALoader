using DXP;
using EasyEDA_Loader;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using PCB;
using SCH;
using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyEDA_LoaderNG
{
    public class LcscBrowserForm : Form
    {
        private readonly WebView2 _browser;
        private readonly WebView2 _hiddenBrowser;

        private readonly Button _btnBack;
        private readonly Button _btnSearch;
        private readonly Button _btnExtractPdf;
        private readonly TextBox _txtSearch;
        private readonly Button _btnImportLib;

        private bool _browserReady;
        private bool _webMessageHooked;

        private TaskCompletionSource<bool>? _extractTcs;
        private bool _extractInProgress;
        private string? _pendingDatasheetName;

        public event EventHandler<string>? UrlChanged;

        public LcscBrowserForm()
        {
            Text = "EasyEDA-LoaderNG – LCSC";
            Width = 1300;
            Height = 850;
            StartPosition = FormStartPosition.CenterScreen;

            // -------------------------------------------------
            // TOP BAR
            // -------------------------------------------------

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44
            };

            _btnBack = new Button
            {
                Text = "◀",
                Width = 36,
                Height = 30,
                Left = 8,
                Top = 7,
                Enabled = false
            };
            _btnBack.Click += (_, __) =>
            {
                if (_browser.CoreWebView2?.CanGoBack == true)
                    _browser.CoreWebView2.GoBack();
            };

            _txtSearch = new TextBox
            {
                Width = 320,
                Height = 26,
                Left = _btnBack.Right + 8,
                Top = 9
            };

            _btnSearch = new Button
            {
                Text = "Search LCSC",
                Width = 80,
                Height = 30,
                Left = _txtSearch.Right + 8,
                Top = 7
            };
            _btnSearch.Click += async (_, __) => await DoSearchAsync();

            _txtSearch.KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await DoSearchAsync();
                }
            };

            _btnExtractPdf = new Button
            {
                Text = "📄 GET DATASHEET (PDF)",
                Width = 220,
                Height = 30,
                Left = _btnSearch.Right + 12,
                Top = 7,
                Enabled = false
            };
            _btnExtractPdf.Click += async (_, __) => await ExtractDatasheetAsync();

            _btnImportLib = new Button
            {
                Text = "⬇ GET COMPONENT LIB",
                Width = 180,
                Height = 30,
                Left = _btnExtractPdf.Right + 12, // On le place à côté
                Top = 7,
                Enabled = false,
                BackColor = System.Drawing.Color.FromArgb(76, 175, 80), // Vert comme ton standalone
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font(DefaultFont, System.Drawing.FontStyle.Bold)
            };

            // L'événement au clic
            _btnImportLib.Click += async (_, __) => await RunComponentImportPipeline();

            topPanel.Controls.Add(_btnBack);
            topPanel.Controls.Add(_txtSearch);
            topPanel.Controls.Add(_btnSearch);
            topPanel.Controls.Add(_btnExtractPdf);
            topPanel.Controls.Add(_btnImportLib);

            // -------------------------------------------------
            // WEBVIEWS
            // -------------------------------------------------

            _browser = new WebView2
            {
                Dock = DockStyle.Fill
            };

            _hiddenBrowser = new WebView2
            {
                Width = 0,
                Height = 0,
                Visible = false
            };

            Controls.Add(_browser);
            Controls.Add(topPanel);
            Controls.Add(_hiddenBrowser);

            Load += async (_, __) => await EnsureBrowserReady();
        }

        // -------------------------------------------------
        // INIT WEBVIEW2
        // -------------------------------------------------

        private async Task EnsureBrowserReady()
        {
            if (_browserReady)
                return;

            await _browser.EnsureCoreWebView2Async();
            await _hiddenBrowser.EnsureCoreWebView2Async();

            _browserReady = true;
            Helper.Log("WebView2 ready (visible + hidden)");

            _browser.CoreWebView2.SourceChanged += (_, __) =>
            {
                string url = _browser.Source?.ToString() ?? "";
                Helper.Log($"URL changed: {url}");
                UrlChanged?.Invoke(this, url);

                bool isProduct = Regex.IsMatch(url, @"C\d+");
                _btnExtractPdf.Enabled = isProduct;
                _btnImportLib.Enabled = isProduct; // Actif uniquement sur une fiche produit
                _btnBack.Enabled = _browser.CoreWebView2.CanGoBack;
            };

            _browser.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                _browser.CoreWebView2.Navigate(e.Uri);
            };

            _browser.CoreWebView2.NavigationCompleted += async (_, __) =>
            {
                await TryAcceptCookiesAsync();
            };

            _hiddenBrowser.CoreWebView2.Settings.IsWebMessageEnabled = true;

            if (!_webMessageHooked)
            {
                _webMessageHooked = true;
                _hiddenBrowser.WebMessageReceived += HiddenBrowser_WebMessageReceived;
            }

            _browser.CoreWebView2.Navigate("https://www.lcsc.com");
        }

        // -------------------------------------------------
        // SEARCH
        // -------------------------------------------------

        private async Task DoSearchAsync()
        {
            string query = _txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
                return;

            string url = BuildLCSCUrl(query);
            await NavigateToAsync(url);
        }

        private static string BuildLCSCUrl(string query)
        {
            query = query.Trim().ToUpperInvariant();

            if (Regex.IsMatch(query, @"^C\d+[A-Z0-9\-]*$"))
                return $"https://www.lcsc.com/product-detail/{query}.html";

            string encoded = Uri.EscapeDataString(query);
            return $"https://www.lcsc.com/search?q={encoded}&s_z=n_{encoded}";
        }

        public async Task NavigateToAsync(string url)
        {
            await EnsureBrowserReady();
            _browser.CoreWebView2.Navigate(url);
        }

        // -------------------------------------------------
        // COOKIES
        // -------------------------------------------------

        private async Task TryAcceptCookiesAsync()
        {
            try
            {
                string js = @"
(() => {
    const buttons = Array.from(document.querySelectorAll('button'));
    const btn = buttons.find(b =>
        b.innerText &&
        (b.innerText.includes('Accept all cookies') ||
         b.innerText.includes('Accept only essential'))
    );
    if (btn) { btn.click(); return 'Cookies accepted'; }
    return 'No cookie dialog';
})();
";
                string result = await _browser.ExecuteScriptAsync(js);
                Helper.Log($"Cookie script: {result}");
            }
            catch (Exception ex)
            {
                Helper.Log($"Cookie script error: {ex}");
            }
        }

        // -------------------------------------------------
        // DATASHEET NAME
        // -------------------------------------------------

        private async Task<string?> ExtractDatasheetNameFromProductPageAsync()
        {
            string script = @"
(() => {
    const span = document.querySelector('a[href^=""/datasheet/""] span');
    if (!span) return null;
    return span.textContent.trim();
})();
";
            string json = await _browser.ExecuteScriptAsync(script);
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return null;

            return JsonSerializer.Deserialize<string>(json);
        }

        // -------------------------------------------------
        // DATASHEET EXTRACTION
        // -------------------------------------------------

        private async Task<bool> ExtractDatasheetAsync()
        {
            if (_extractInProgress)
                return false;

            string currentUrl = _browser.Source?.ToString() ?? "";
            var match = Regex.Match(currentUrl, @"C\d+");

            if (!match.Success)
            {
                MessageBox.Show(
                    "Impossible de détecter le SKU (Cxxxx).",
                    "Erreur LCSC",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            string sku = match.Value;

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

            _hiddenBrowser.CoreWebView2.Navigate(
                $"https://www.lcsc.com/datasheet/{sku}.pdf");

            string script = @"
(async () => {
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    let iframe = null, attempts = 0;

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
        window.chrome.webview.postMessage({ type: 'PDF_ERROR', message: 'PDF introuvable' });
        return;
    }

    try {
        const res = await fetch(targetUrl);
        if (!res.ok) throw new Error('HTTP ' + res.status);

        const blob = await res.blob();
        const reader = new FileReader();
        reader.onloadend = () => {
            const base64 = reader.result.split(',')[1];
            window.chrome.webview.postMessage({ type: 'PDF_CONTENT', payload: base64 });
        };
        reader.readAsDataURL(blob);
    } catch (e) {
        window.chrome.webview.postMessage({ type: 'PDF_ERROR', message: e.toString() });
    }
})();
";
            await Task.Delay(300);
            await _hiddenBrowser.CoreWebView2.ExecuteScriptAsync(script);

            bool result = await _extractTcs.Task;

            _extractTcs = null;
            _pendingDatasheetName = null;
            _extractInProgress = false;

            return result;
        }

        // -------------------------------------------------
        // PDF MESSAGE HANDLER
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
                    string msg = root.GetProperty("message").GetString() ?? "Erreur PDF";
                    this.Invoke(() =>
                    {
                        MessageBox.Show(msg, "Erreur PDF",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                    _extractTcs.TrySetResult(false);
                }
                else if (type == "PDF_CONTENT")
                {
                    byte[] bytes = Convert.FromBase64String(
                        root.GetProperty("payload").GetString() ?? "");

                    string filename =
                        !string.IsNullOrWhiteSpace(_pendingDatasheetName)
                            ? _pendingDatasheetName + ".pdf"
                            : "datasheet.pdf";

                    this.Invoke(() =>
                    {
                        string docPath = AltiumApi.GetActiveProjectPath();
                        Helper.Log($"[DEBUG] Chemin du document détecté : '{docPath}'");

                        using var dlg = new SaveFileDialog
                        {
                            Filter = "PDF (*.pdf)|*.pdf",
                            FileName = filename,
                            InitialDirectory = docPath, // C'est ici que la magie opère
                            RestoreDirectory = false
                        };

                        if (dlg.ShowDialog(this) == DialogResult.OK)
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
                _extractTcs.TrySetResult(false);
            }
        }

        // -------------------------------------------------
        // GET COMPONENT LIBRRY HANDLER
        // -------------------------------------------------
        private async Task RunComponentImportPipeline()
        {
            string url = _browser.Source?.ToString() ?? "";
            var match = Regex.Match(url, @"C\d+");
            if (!match.Success) return;

            string componentId = match.Value;

            try
            {
                // 1. On vérifie qu'on est bien sur un Schématique dans Altium
                var currentDoc = AltiumApi.GlobalVars.Client.GetCurrentView()?.GetOwnerDocument();
                if (currentDoc == null)
                {
                    MessageBox.Show("Vous devez avoir un document Schématique ouvert dans Altium.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 2. On lance le chargement (on réutilise ton moteur EasyedaApi)
                var cts = new CancellationTokenSource();
                var api = new EasyedaApi();

                // On récupère les données JSON
                var root = await api.GetComponentJsonAsync(componentId, cts.Token);
                if (root?.Component == null) throw new Exception("Données EasyEDA introuvables.");

                var owner_id = root.Component.Owner.Uuid;
                var ee_footprint = root.Component.PackageDetail.Footprint;
                var ee_symbol = root.Component.Symbol;
                string package = ee_footprint.Head.Parameters.Package;
                EeFootprint3dModel model = ee_footprint.GetModel();

                // 3. On prépare les chemins (on garde ta logique MyDocuments\AltiumEE)
                string libraryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AltiumEE");
                Directory.CreateDirectory(libraryPath);
                string pcbLibraryPath = Path.Combine(libraryPath, "EasyEDA.pcblib");
                string schLibraryPath = Path.Combine(libraryPath, "EasyEDA.schlib");

                // 4. Exécution du pipeline Altium (PCB)
                var pcbDocument = AltiumApi.GlobalVars.Client.OpenDocument("PcbLib", pcbLibraryPath);
                AltiumApi.GlobalVars.Client.ShowDocument(pcbDocument);
                var pcbLib = AltiumApi.GlobalVars.PCBServer.GetCurrentPCBLibrary();

                if (pcbLib.GetComponentByName(package) == null)
                {
                    // Prefetch du modèle 3D
                    byte[] rawModelData = model != null ? await api.LoadRawModelAsync(model.Uuid, cts.Token) : null;

                    var libComp = EEPCB.CreateFootprintInLib(package, root.Component.PackageDetail.Title);
                    AltiumApi.GlobalVars.PCBServer.PreProcess();
                    var fpCtx = new EeFootprintContext
                    {
                        Box = ee_footprint.BoundingBox,
                        Layers = ee_footprint.Layers,
                        RawModelTask = Task.FromResult(rawModelData)
                    };
                    ee_footprint.AddToComponent(libComp, fpCtx);
                    AltiumApi.GlobalVars.PCBServer.PostProcess();
                    pcbDocument.DoFileSave("PcbLib");
                }

                // 5. Exécution du pipeline Altium (SCH)
                var schDocument = AltiumApi.GlobalVars.Client.OpenDocument("SchLib", schLibraryPath);
                AltiumApi.GlobalVars.Client.ShowDocument(schDocument);

                var productInfo = await api.GetProductInfoAsync(componentId, owner_id);
                string partName = ee_symbol.Head.Parameters.Name;

                (var schLib, var component) = EESCH.CreateComponentInLib(partName, productInfo?.Description ?? partName, ee_symbol.Head.Parameters.Pre);

                SymbolDrawing.CreateComponent(schLib, component, pcbLibraryPath, package, ee_symbol);

                // Ajout des paramètres (LCSC, Supplier, etc.)
                if (productInfo?.Parameters != null)
                {
                    foreach (var kvp in productInfo.Parameters)
                        if (!string.IsNullOrEmpty(kvp.Key)) EESCH.AddParameter(component, kvp.Key, kvp.Value ?? "");
                }

                schDocument.DoFileSave("SchLib");

                // 6. Finalisation et placement
                AltiumApi.GlobalVars.Client.ShowDocument(currentDoc);
                AltiumApi.GlobalVars.Client.CloseDocument(pcbDocument);
                AltiumApi.GlobalVars.Client.CloseDocument(schDocument);

                // On place le composant sur la feuille
                var newComponent = AltiumApi.GlobalVars.SCHServer.LoadComponentFromLibrary(partName, schLibraryPath);
                var currentSheet = AltiumApi.GlobalVars.SCHServer.GetCurrentSchDocument();
                currentSheet.AddSchObject(newComponent);
                newComponent.MoveToXY(0, 0); // Position par défaut 0,0
                currentSheet.GraphicallyInvalidate();

                //MessageBox.Show($"Composant {partName} importé avec succès !", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // --- AJOUT : Fermeture automatique pour redonner le focus à Altium ---
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'import : {ex.Message}", "Erreur Pipeline", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



    }
}
