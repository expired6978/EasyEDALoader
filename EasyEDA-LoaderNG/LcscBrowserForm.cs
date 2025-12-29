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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyEDA_LoaderNG
{
    public class LcscBrowserForm : Form
    {
        private readonly WebView2 _browser;
        private readonly WebView2 _hiddenBrowser;

        private readonly Button _btnBack;
        private readonly Button _btnSearchLcsc;
        private readonly Button _btnSearchJlc;
        private readonly Button _btnExtractPdf;
        private readonly TextBox _txtSearch;
        private readonly Button _btnImportLib;
        private readonly CheckBox _chkDebug;

        // Visual Feedbacks
        private readonly ProgressBar _progressBar;
        private readonly Label _lblStatus;

        private bool _browserReady;
        private bool _webMessageHooked;

        private TaskCompletionSource<bool>? _extractTcs;
        private bool _extractInProgress;
        private string? _pendingDatasheetName;

        public event EventHandler<string>? UrlChanged;

        public LcscBrowserForm()
        {
            Text = "EasyEDA-LoaderNG – LCSC Browser";
            Width = 1300;
            Height = 850;
            StartPosition = FormStartPosition.CenterScreen;

            // -------------------------------------------------
            // TOP BAR
            // -------------------------------------------------
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 44 };

            _btnBack = new Button { Text = "◀", Width = 36, Height = 30, Left = 8, Top = 7, Enabled = false };
            _btnBack.Click += (_, __) => { if (_browser.CoreWebView2?.CanGoBack == true) _browser.CoreWebView2.GoBack(); };

            _txtSearch = new TextBox { Width = 320, Height = 26, Left = _btnBack.Right + 8, Top = 9 };

            // --- Bouton Recherche LCSC ---
            _btnSearchLcsc = new Button
            {
                Text = "LCSC",
                Width = 90,
                Height = 30,
                Left = _txtSearch.Right + 8,
                Top = 7
            };
            _btnSearchLcsc.Click += _btnSearchLcsc_Click; // Liaison directe à la méthode

            // --- Bouton Recherche JLCPCB ---
            _btnSearchJlc = new Button
            {
                Text = "JLCPCB",
                Width = 110, // Un peu plus large pour le texte JLC
                Height = 30,
                Left = _btnSearchLcsc.Right + 8,
                Top = 7
            };
            _btnSearchJlc.Click += _btnSearchJlc_Click; // Liaison directe à la méthode

            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _btnSearchLcsc_Click(s, e); // On force le clic sur LCSC
                }
            };

            _btnExtractPdf = new Button { Text = "📄 GET DATASHEET (PDF)", Width = 220, Height = 30, Left = _btnSearchJlc.Right + 12, Top = 7, Enabled = false };
            _btnExtractPdf.Click += async (_, __) => await ExtractDatasheetAsync();

            _btnImportLib = new Button
            {
                Text = "⬇ GET LIB.",
                Width = 180,
                Height = 30,
                Left = _btnExtractPdf.Right + 12,
                Top = 7,
                Enabled = false,
                BackColor = System.Drawing.Color.FromArgb(76, 175, 80),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font(DefaultFont, System.Drawing.FontStyle.Bold)
            };
            _btnImportLib.Click += async (_, __) => await RunComponentImportPipeline();

            _chkDebug = new CheckBox
            {
                Text = "Debug Logs",
                AutoSize = true,
                Left = _btnImportLib.Right + 12,
                Top = 9,
                Checked = false // Désactivé par défaut (Mode Silence)
            };

            topPanel.Controls.Add(_btnBack);
            topPanel.Controls.Add(_txtSearch);
            topPanel.Controls.Add(_btnSearchLcsc);
            topPanel.Controls.Add(_btnSearchJlc);
            topPanel.Controls.Add(_btnExtractPdf);
            topPanel.Controls.Add(_btnImportLib);
            topPanel.Controls.Add(_chkDebug);

            // -------------------------------------------------
            // STATUS BAR & PROGRESS (BOTTOM)
            // -------------------------------------------------
            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 6,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible = false
            };

            _lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Text = "Ready",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0),
                BackColor = System.Drawing.Color.WhiteSmoke,
                Font = new System.Drawing.Font("Segoe UI", 8.25F),
                Visible = false
            };

            // -------------------------------------------------
            // WEBVIEWS
            // -------------------------------------------------
            _browser = new WebView2 { Dock = DockStyle.Fill };
            _hiddenBrowser = new WebView2 { Width = 0, Height = 0, Visible = false };

            Controls.Add(_browser);
            Controls.Add(topPanel);
            Controls.Add(_lblStatus);
            Controls.Add(_progressBar);
            Controls.Add(_hiddenBrowser);

            Load += async (_, __) => await EnsureBrowserReady();
        }

        private void LogDebug(string message)
        {
            if (_chkDebug.Checked)
            {
                Helper.Log(message);
            }
        }

        private async Task EnsureBrowserReady()
        {
            if (_browserReady) return;
            await _browser.EnsureCoreWebView2Async();
            await _hiddenBrowser.EnsureCoreWebView2Async();
            _browserReady = true;

            // --- LE NETTOYEUR ULTIME (Popup + Avatar) ---
            string cleanerScript = @"
        setInterval(() => {
            try {
                // CIBLE 1 : Le gros popup (Déjà fait)
                document.querySelectorAll('.chat-icon-popover').forEach(el => {
                    el.style.display = 'none';
                    el.style.visibility = 'hidden';
                });

                // CIBLE 2 : La médaille / L'avatar (NOUVEAU)
                // On vise la classe que tu as trouvée
                document.querySelectorAll('.chat-avatar').forEach(el => {
                    el.style.display = 'none';
                    el.style.visibility = 'hidden';
                });

                // CIBLE 3 : L'Arme Nucléaire (Sécurité)
                // Si jamais ils changent les classes, on cherche l'image par son URL technique
                // et on cache son parent.
                document.querySelectorAll('img').forEach(img => {
                    if (img.src && img.src.includes('overseas-im-platform')) {
                        // On remonte au conteneur (la div qui l'entoure)
                        if (img.parentElement) img.parentElement.style.display = 'none';
                        // Au cas où, on cache l'image elle-même
                        img.style.display = 'none';
                    }
                });

            } catch (e) { }
        }, 500);
    ";

            await _browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(cleanerScript);

            // --- LE RESTE EST INCHANGÉ ---
            _browser.CoreWebView2.SourceChanged += (_, __) =>
            {
                string url = _browser.Source?.ToString() ?? "";
                UrlChanged?.Invoke(this, url);

                string sku = ExtractSkuFromUrl(url);
                bool isProductPage = !string.IsNullOrEmpty(sku) && (url.Contains("product-detail") || url.Contains("partdetail"));

                _btnExtractPdf.Enabled = isProductPage;
                _btnImportLib.Enabled = isProductPage;
                _btnBack.Enabled = _browser.CoreWebView2.CanGoBack;

                LogDebug($"[NAV] URL: {url} | Buttons Enabled: {isProductPage}");
            };

            _browser.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; _browser.CoreWebView2.Navigate(e.Uri); };
            _browser.CoreWebView2.NavigationCompleted += async (_, __) => await TryAcceptCookiesAsync();

            _hiddenBrowser.CoreWebView2.Settings.IsWebMessageEnabled = true;

            if (!_webMessageHooked)
            {
                _webMessageHooked = true;
                _hiddenBrowser.WebMessageReceived += HiddenBrowser_WebMessageReceived;
            }

            _browser.CoreWebView2.Navigate("https://www.lcsc.com");
        }

        private void _btnSearchLcsc_Click(object sender, EventArgs e)
        {
            string query = _txtSearch.Text.Trim();
            string targetUrl = string.IsNullOrEmpty(query)
                ? "https://www.lcsc.com"
                : $"https://www.lcsc.com/search?q={Uri.EscapeDataString(query)}";

            LogDebug($"[UI] Navigating to LCSC: {targetUrl}");
            _browser.Source = new Uri(targetUrl);
        }

        private void _btnSearchJlc_Click(object sender, EventArgs e)
        {
            string query = _txtSearch.Text.Trim();
            string targetUrl;

            if (string.IsNullOrEmpty(query))
            {
                // Pas de texte : on va sur l'accueil des composants
                targetUrl = "https://jlcpcb.com/parts/all-electronic-components";
                LogDebug("[UI] Loading JLCPCB Parts Home");
            }
            else
            {
                // Texte présent : on utilise le format que tu as trouvé
                targetUrl = $"https://jlcpcb.com/parts/componentSearch?searchTxt={Uri.EscapeDataString(query)}";
                LogDebug($"[UI] Searching JLCPCB: {query}");
            }

            _browser.Source = new Uri(targetUrl);
        }

        private static string BuildLCSCUrl(string query)
        {
            query = query.Trim().ToUpperInvariant();
            if (Regex.IsMatch(query, @"^C\d+[A-Z0-9\-]*$")) return $"https://www.lcsc.com/product-detail/{query}.html";
            return $"https://www.lcsc.com/search?q={Uri.EscapeDataString(query)}";
        }

        private async Task TryAcceptCookiesAsync()
        {
            try
            {
                string js = @"(() => { const btn = Array.from(document.querySelectorAll('button')).find(b => b.innerText && (b.innerText.includes('Accept all cookies') || b.innerText.includes('Accept only essential'))); if (btn) { btn.click(); return 'OK'; } return 'No'; })();";
                await _browser.ExecuteScriptAsync(js);
            }
            catch { }
        }

        private async Task<string?> ExtractDatasheetNameFromProductPageAsync()
        {
            string url = _browser.Source?.ToString() ?? "";
            bool isJLCPCB = url.Contains("jlcpcb.com");
            string script;

            if (isJLCPCB)
            {
                // Stratégie JLCPCB : On prend le MFR.Part # (ex: RP2040)
                script = @"(() => { 
            const name = Array.from(document.querySelectorAll('dt'))
                            .find(el => el.innerText.includes('MFR.Part #'))
                            ?.nextElementSibling?.innerText;
            return name ? name.trim() : null; 
        })();";
            }
            else
            {
                // Stratégie LCSC (Ta méthode actuelle qui marche bien)
                // Elle tente de chopper le nom du fichier affiché à côté de l'icône PDF
                script = @"(() => { 
            const span = document.querySelector('a[href^=""/datasheet/""] span'); 
            // Si pas de span spécifique, on fallback sur le titre principal du produit
            const title = document.querySelector('.product-title-h1')?.innerText; 
            return span ? span.textContent.trim() : (title ? title.trim() : null); 
        })();";
            }

            string json = await _browser.ExecuteScriptAsync(script);
            string extractedName = (string.IsNullOrWhiteSpace(json) || json == "null")
                ? null
                : JsonSerializer.Deserialize<string>(json);

            // Petit nettoyage de sécurité pour le nom de fichier (enlève les / \ : etc.)
            if (!string.IsNullOrEmpty(extractedName))
            {
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    extractedName = extractedName.Replace(c, '_');
                }
            }

            return extractedName;
        }

        private async Task<bool> ExtractDatasheetAsync()
        {
            if (_extractInProgress) return false;

            // --- SETUP ---
            string currentUrl = _browser.Source?.ToString() ?? "";

            string sku = ExtractSkuFromUrl(currentUrl);

            // Si sku est null ou vide, c'est qu'on est pas sur une page produit valide
            if (string.IsNullOrEmpty(sku)) return false;

            _extractInProgress = true;
            _progressBar.Visible = true;
            _lblStatus.Visible = true;
            _lblStatus.Text = "Processing...";

            try
            {
                // Nom et Nettoyage
                _pendingDatasheetName = await ExtractDatasheetNameFromProductPageAsync();
                if (string.IsNullOrEmpty(_pendingDatasheetName)) _pendingDatasheetName = sku;

                string cleanName = _pendingDatasheetName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFileNameWithoutExtension(_pendingDatasheetName)
                    : _pendingDatasheetName;

                // --- CHECK LOCAL ---
                string projectDir = AltiumApi.GetActiveProjectPath();
                if (string.IsNullOrEmpty(projectDir)) projectDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                string datasheetsDir = Path.Combine(projectDir, "Datasheets");
                string localFilePath = Path.Combine(datasheetsDir, $"{cleanName}.pdf");

                // Cas du doublon : On demande si on écrase
                if (File.Exists(localFilePath))
                {
                    DialogResult result = MessageBox.Show(this,
                        $"The datasheet '{cleanName}.pdf' is already in the directory.\n\nDo you want to overwrite it?",
                        "Duplicate Detected",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.No)
                    {
                        LogDebug("[C#] Overwrite cancelled by user.");
                        return true;
                    }
                }

                // --- DOWNLOAD ---
                _extractTcs = new TaskCompletionSource<bool>();
                _lblStatus.Text = "Downloading PDF...";

                string navUrl = $"https://www.lcsc.com/datasheet/{sku}.pdf";
                LogDebug($"[C#] Navigating Hidden Browser to: {navUrl}");

                _hiddenBrowser.CoreWebView2.Navigate(navUrl);

                // --- SCRIPT JS (Bavard, mais filtré par C#) ---
                string script = @"(async () => { 
            const log = (msg) => window.chrome.webview.postMessage({ type: 'LOG', message: msg });
            const sleep = ms => new Promise(r => setTimeout(r, ms)); 
            
            let attempts = 0; 
            log('--- JS START --- Searching for iframe...');

            while (attempts < 50) { 
                if (document.querySelector('iframe[src*="".pdf""]')) break; 
                await sleep(200); 
                attempts++; 
            } 
            
            let targetUrl = document.querySelector('iframe[src*="".pdf""]')?.src; 
            
            if (targetUrl) {
                log('RAW Iframe Src found: ' + targetUrl);
                
                // --- CORRECTIF LCSC VIEWER ---
                if (targetUrl.includes('viewer.html') && targetUrl.includes('?file=')) {
                    log('⚠️ VIEWER DETECTED! Extracting real URL...');
                    try {
                        const newUrl = new URL(targetUrl).searchParams.get('file');
                        if (newUrl) targetUrl = newUrl;
                    } catch (e) { log('❌ Extraction Error: ' + e.message); }
                }
            }

            // Fallback
            if (!targetUrl && window.location.href.endsWith('.pdf')) {
                 log('⚠️ Fallback: Using window.location.');
                 targetUrl = window.location.href;
            }

            if (!targetUrl) { 
                window.chrome.webview.postMessage({ type: 'PDF_ERROR', message: 'PDF URL not found.' }); 
                return; 
            } 
            
            try { 
                log('Fetching: ' + targetUrl);
                const res = await fetch(targetUrl); 
                const blob = await res.blob(); 
                
                if (blob.type.includes('text/html')) {
                     window.chrome.webview.postMessage({ type: 'PDF_ERROR', message: 'CRITICAL: Downloaded content is HTML! Extraction failed.' });
                     return;
                }

                const reader = new FileReader(); 
                reader.onloadend = () => { 
                    log('Sending payload...');
                    window.chrome.webview.postMessage({ type: 'PDF_CONTENT', payload: reader.result.split(',')[1] }); 
                }; 
                reader.readAsDataURL(blob); 
            } catch (e) { 
                window.chrome.webview.postMessage({ type: 'PDF_ERROR', message: e.toString() }); 
            } 
        })();";

                await Task.Delay(800);
                LogDebug("[C#] Injecting Script...");
                await _hiddenBrowser.CoreWebView2.ExecuteScriptAsync(script);

                return await _extractTcs.Task;
            }
            catch (Exception ex)
            {
                // On logue toujours les vraies erreurs
                Helper.Log($"[PDF ERROR] {ex.Message}");
                return false;
            }
            finally
            {
                _extractInProgress = false;
                _progressBar.Visible = false;
                _lblStatus.Visible = false;
            }
        }
        private void HiddenBrowser_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (!_extractInProgress || _extractTcs == null) return;
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;
                string type = root.GetProperty("type").GetString() ?? "";

                // --- FILTRE LOGS ---
                if (type == "LOG")
                {
                    // On ne logue que si la case est cochée
                    if (_chkDebug.Checked)
                    {
                        string msg = root.GetProperty("message").GetString() ?? "";
                        Helper.Log($"[JS-SPY] {msg}");
                    }
                    return;
                }
                // -------------------

                if (type == "PDF_ERROR")
                {
                    string errMsg = root.GetProperty("message").GetString();
                    Helper.Log($"[JS-ERROR] {errMsg}"); // Toujours loguer les erreurs
                    MessageBox.Show(errMsg, "PDF Error");
                    _extractTcs.TrySetResult(false);
                }
                else if (type == "PDF_CONTENT")
                {
                    byte[] bytes = Convert.FromBase64String(root.GetProperty("payload").GetString() ?? "");
                    string filename = !string.IsNullOrWhiteSpace(_pendingDatasheetName) ? _pendingDatasheetName + ".pdf" : "datasheet.pdf";

                    LogDebug($"[C#] PDF Received! Size: {bytes.Length} bytes.");

                    this.Invoke(() => {
                        string projectDir = AltiumApi.GetActiveProjectPath();
                        if (string.IsNullOrEmpty(projectDir)) projectDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                        string docPath = Path.Combine(projectDir, "Datasheets");
                        if (!Directory.Exists(docPath)) Directory.CreateDirectory(docPath);

                        LogDebug($"[C#] Saving to: {docPath}");

                        using var dlg = new SaveFileDialog
                        {
                            Filter = "PDF Files (*.pdf)|*.pdf",
                            FileName = filename,
                            InitialDirectory = docPath
                        };

                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            File.WriteAllBytes(dlg.FileName, bytes);
                            LogDebug($"[C#] Saved successfully.");
                            _extractTcs.TrySetResult(true);
                        }
                        else
                        {
                            LogDebug("[C#] Save cancelled.");
                            _extractTcs.TrySetResult(false);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Helper.Log($"[C# ERROR] Message Handler Fault: {ex.Message}");
                _extractTcs.TrySetResult(false);
            }
        }

        private async Task RunComponentImportPipeline()
        {
            // 1. EXTRACTION DU SKU ET DU SITE
            string url = _browser.Source?.ToString() ?? "";
            bool isJLCPCB = url.Contains("jlcpcb.com");

            string componentId = ExtractSkuFromUrl(url);
            if (string.IsNullOrEmpty(componentId)) return;

            // UI Feedback
            _btnSearchLcsc.Enabled = false;
            _btnSearchJlc.Enabled = false;
            _progressBar.Visible = true;
            _lblStatus.Visible = true;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                // ---------------------------------------------------------
                // 2. DOM SCRAPING
                // ---------------------------------------------------------
                _lblStatus.Text = "Extracting component name...";
                LogDebug($"[PIPELINE] Starting import for SKU: {componentId} on {(isJLCPCB ? "JLCPCB" : "LCSC")}");

                string jsSelector;
                if (isJLCPCB)
                {
                    jsSelector = @"Array.from(document.querySelectorAll('dt'))
                    .find(el => el.innerText.includes('MFR.Part #'))
                    ?.nextElementSibling?.innerText";
                }
                else
                {
                    jsSelector = "document.querySelector('span.major2--text')?.innerText";
                }

                string partNameRaw = await _browser.ExecuteScriptAsync(jsSelector);
                string partName = partNameRaw?.Trim('"').Replace("\\\"", "\"").Trim();

                // Fallback
                if (string.IsNullOrEmpty(partName) || partName == "null")
                {
                    partName = componentId;
                    LogDebug($"[DOM] ⚠️ Name not found via JS, using SKU as fallback: {partName}");
                }
                else
                {
                    LogDebug($"[DOM] ✅ Name extracted: {partName}");
                }

                // ---------------------------------------------------------
                // 3. CHECK LIBRAIRIE & CHEMINS
                // ---------------------------------------------------------
                string baseDirectory = AltiumApi.GetActiveProjectPath();
                if (string.IsNullOrEmpty(baseDirectory))
                    baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AltiumEE");

                LogDebug($"[PATH] Project Root: {baseDirectory}");

                string libraryFolder = Path.Combine(baseDirectory, "Library");
                if (!Directory.Exists(libraryFolder)) Directory.CreateDirectory(libraryFolder);

                string schLibraryPath = Path.Combine(libraryFolder, "EasyEDA.schlib");
                string pcbLibraryPath = Path.Combine(libraryFolder, "EasyEDA.pcblib");

                bool shouldDownload = true;

                // Check existant
                SCH.ISch_Component existingComp = null;
                try
                {
                    existingComp = AltiumApi.GlobalVars.SCHServer.LoadComponentFromLibrary(partName, schLibraryPath);
                }
                catch { }

                if (existingComp != null)
                {
                    LogDebug($"[CHECK] Component '{partName}' found in local library.");
                    DialogResult result = MessageBox.Show(this,
                        $"The component '{partName}' already exists in your local library.\n\nDo you want to re-download and overwrite it?",
                        "Component Already Exists",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.No)
                    {
                        shouldDownload = false;
                        LogDebug($"[PIPELINE] User chose to use existing component: {partName}");
                    }
                    else
                    {
                        LogDebug($"[PIPELINE] User chose to OVERWRITE component: {partName}");
                    }
                }

                // ---------------------------------------------------------
                // 4. DOWNLOAD & GÉNÉRATION
                // ---------------------------------------------------------
                if (shouldDownload)
                {
                    _lblStatus.Text = $"Downloading data for {partName}...";
                    LogDebug("[API] Querying EasyEDA API...");

                    var cts = new CancellationTokenSource();
                    var api = new EasyedaApi();
                    var root = await api.GetComponentJsonAsync(componentId, cts.Token);

                    if (root?.Component == null) throw new Exception("EasyEDA component data not found via API.");

                    var comp = root.Component;
                    var ee_footprint = comp.PackageDetail.Footprint;
                    var ee_symbol = comp.Symbol;
                    string package = ee_footprint.Head.Parameters.Package;

                    LogDebug($"[API] Data received. Package: {package}");

                    // --- A. PCB Footprint ---
                    var pcbDocument = AltiumApi.GlobalVars.Client.OpenDocument("PcbLib", pcbLibraryPath);
                    AltiumApi.GlobalVars.Client.ShowDocument(pcbDocument);
                    var pcbLib = AltiumApi.GlobalVars.PCBServer.GetCurrentPCBLibrary();

                    if (pcbLib.GetComponentByName(package) == null)
                    {
                        _lblStatus.Text = $"Generating Footprint: {package}...";
                        LogDebug($"[PCB] Creating new footprint: {package}");

                        var model = ee_footprint.GetModel();
                        byte[] rawModelData = model != null ? await api.LoadRawModelAsync(model.Uuid, cts.Token) : null;

                        var libComp = EEPCB.CreateFootprintInLib(package, comp.PackageDetail.Title);
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
                    else
                    {
                        LogDebug($"[PCB] Footprint '{package}' already exists. Skipping generation.");
                    }
                    AltiumApi.GlobalVars.Client.CloseDocument(pcbDocument);

                    // --- B. SCH Symbol ---
                    var schDocument = AltiumApi.GlobalVars.Client.OpenDocument("SchLib", schLibraryPath);
                    AltiumApi.GlobalVars.Client.ShowDocument(schDocument);

                    _lblStatus.Text = $"Generating Symbol: {partName}...";
                    LogDebug($"[SCH] Creating/Updating symbol: {partName}");

                    var productInfo = await api.GetProductInfoAsync(componentId, comp.Owner.Uuid);

                    (var lib, var component) = EESCH.CreateComponentInLib(partName, productInfo?.Description ?? partName, ee_symbol.Head.Parameters.Pre);

                    if (lib != null && component != null)
                    {
                        SymbolDrawing.CreateComponent(lib, component, pcbLibraryPath, package, ee_symbol);
                        if (productInfo?.Parameters != null)
                        {
                            foreach (var kvp in productInfo.Parameters)
                                if (!string.IsNullOrEmpty(kvp.Key)) EESCH.AddParameter(component, kvp.Key, kvp.Value ?? "");
                        }
                        schDocument.DoFileSave("SchLib");
                    }
                    AltiumApi.GlobalVars.Client.CloseDocument(schDocument);
                }

                // ---------------------------------------------------------
                // 5. PLACEMENT FINAL
                // ---------------------------------------------------------
                _lblStatus.Text = "Placing component...";

                var currentDoc = AltiumApi.GlobalVars.Client.GetCurrentView()?.GetOwnerDocument();
                if (currentDoc != null) AltiumApi.GlobalVars.Client.ShowDocument(currentDoc);

                var currentSheet = AltiumApi.GlobalVars.SCHServer.GetCurrentSchDocument();
                var newComponent = AltiumApi.GlobalVars.SCHServer.LoadComponentFromLibrary(partName, schLibraryPath);

                if (newComponent != null && currentSheet != null)
                {
                    currentSheet.AddSchObject(newComponent);
                    newComponent.MoveToXY(10, 10);
                    currentSheet.GraphicallyInvalidate();

                    LogDebug($"[PIPELINE] ✅ SUCCESS: {partName} placed on sheet.");
                }
                else
                {
                    // Erreur logique, on la garde en LogDebug car ce n'est pas un crash C#
                    LogDebug("[PIPELINE] ⚠️ Warning: Could not place component (Sheet or Component is null).");
                }

                this.Close();
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                _progressBar.Visible = false;
                _lblStatus.Visible = false;
                _btnSearchLcsc.Enabled = true;
                _btnSearchJlc.Enabled = true;

                // ERREUR CRITIQUE = Helper.Log (Toujours visible)
                Helper.Log($"[PIPELINE ERROR] {ex.Message}\nStack: {ex.StackTrace}");
                MessageBox.Show($"Import failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        /// <summary>
        /// Extrait le SKU (ex: C1091) d'une URL de manière intelligente.
        /// Ignore les faux positifs comme "RC02" dans le nom du composant.
        /// </summary>
        private string ExtractSkuFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            // (?<![a-zA-Z]) signifie : "Qui n'est PAS précédé d'une lettre"
            // Cela évite de matcher "RC02" (car C est précédé de R)
            // Mais ça matche "/C1091" (car C est précédé de /) ou "_C1091" (précédé de _)
            var matches = Regex.Matches(url, @"(?<![a-zA-Z])C\d+");

            if (matches.Count > 0)
            {
                // On prend le DERNIER match trouvé dans l'URL.
                // C'est souvent plus sûr sur JLCPCB où le SKU est tout à la fin.
                return matches[matches.Count - 1].Value;
            }

            return null;
        }
    }
}