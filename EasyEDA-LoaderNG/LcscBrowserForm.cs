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
        private readonly Button _btnSearch;
        private readonly Button _btnExtractPdf;
        private readonly TextBox _txtSearch;
        private readonly Button _btnImportLib;

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

            _btnSearch = new Button { Text = "Search LCSC", Width = 90, Height = 30, Left = _txtSearch.Right + 8, Top = 7 };
            _btnSearch.Click += async (_, __) => await DoSearchAsync();

            _txtSearch.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await DoSearchAsync(); } };

            _btnExtractPdf = new Button { Text = "📄 GET DATASHEET (PDF)", Width = 220, Height = 30, Left = _btnSearch.Right + 12, Top = 7, Enabled = false };
            _btnExtractPdf.Click += async (_, __) => await ExtractDatasheetAsync();

            _btnImportLib = new Button
            {
                Text = "⬇ GET COMPONENT LIB",
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

            topPanel.Controls.Add(_btnBack);
            topPanel.Controls.Add(_txtSearch);
            topPanel.Controls.Add(_btnSearch);
            topPanel.Controls.Add(_btnExtractPdf);
            topPanel.Controls.Add(_btnImportLib);

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

        private async Task EnsureBrowserReady()
        {
            if (_browserReady) return;
            await _browser.EnsureCoreWebView2Async();
            await _hiddenBrowser.EnsureCoreWebView2Async();
            _browserReady = true;

            _browser.CoreWebView2.SourceChanged += (_, __) =>
            {
                string url = _browser.Source?.ToString() ?? "";
                UrlChanged?.Invoke(this, url);
                bool isProduct = Regex.IsMatch(url, @"C\d+");
                _btnExtractPdf.Enabled = isProduct;
                _btnImportLib.Enabled = isProduct;
                _btnBack.Enabled = _browser.CoreWebView2.CanGoBack;
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

        private async Task DoSearchAsync()
        {
            string query = _txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;
            _browser.CoreWebView2.Navigate(BuildLCSCUrl(query));
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
            string json = await _browser.ExecuteScriptAsync(@"(() => { const span = document.querySelector('a[href^=""/datasheet/""] span'); return span ? span.textContent.trim() : null; })();");
            return (string.IsNullOrWhiteSpace(json) || json == "null") ? null : JsonSerializer.Deserialize<string>(json);
        }

        private async Task<bool> ExtractDatasheetAsync()
        {
            if (_extractInProgress) return false;
            string currentUrl = _browser.Source?.ToString() ?? "";
            var match = Regex.Match(currentUrl, @"C\d+");
            if (!match.Success) return false;

            _extractInProgress = true;
            _progressBar.Visible = true;
            _lblStatus.Visible = true;
            _lblStatus.Text = "Preparing PDF extraction...";

            try
            {
                _pendingDatasheetName = await ExtractDatasheetNameFromProductPageAsync();
                _extractTcs = new TaskCompletionSource<bool>();
                _hiddenBrowser.CoreWebView2.Navigate($"https://www.lcsc.com/datasheet/{match.Value}.pdf");

                string script = @"(async () => { const sleep = ms => new Promise(r => setTimeout(r, ms)); let attempts = 0; while (attempts < 50) { if (document.querySelector('iframe[src$="".pdf""]')) break; await sleep(200); attempts++; } const targetUrl = document.querySelector('iframe[src$="".pdf""]')?.src || (window.location.href.endsWith('.pdf') ? window.location.href : null); if (!targetUrl) { window.chrome.webview.postMessage({ type: 'PDF_ERROR', message: 'PDF not found' }); return; } try { const res = await fetch(targetUrl); const blob = await res.blob(); const reader = new FileReader(); reader.onloadend = () => { window.chrome.webview.postMessage({ type: 'PDF_CONTENT', payload: reader.result.split(',')[1] }); }; reader.readAsDataURL(blob); } catch (e) { window.chrome.webview.postMessage({ type: 'PDF_ERROR', message: e.toString() }); } })();";
                await Task.Delay(500);
                await _hiddenBrowser.CoreWebView2.ExecuteScriptAsync(script);
                return await _extractTcs.Task;
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

                if (type == "PDF_ERROR")
                {
                    MessageBox.Show(root.GetProperty("message").GetString(), "PDF Error");
                    _extractTcs.TrySetResult(false);
                }
                else if (type == "PDF_CONTENT")
                {
                    byte[] bytes = Convert.FromBase64String(root.GetProperty("payload").GetString() ?? "");
                    string filename = !string.IsNullOrWhiteSpace(_pendingDatasheetName) ? _pendingDatasheetName + ".pdf" : "datasheet.pdf";

                    this.Invoke(() => {
                        string docPath = AltiumApi.GetActiveProjectPath();
                        using var dlg = new SaveFileDialog { Filter = "PDF Files (*.pdf)|*.pdf", FileName = filename, InitialDirectory = docPath };
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            File.WriteAllBytes(dlg.FileName, bytes);
                            _extractTcs.TrySetResult(true);
                        }
                        else _extractTcs.TrySetResult(false);
                    });
                }
            }
            catch { _extractTcs.TrySetResult(false); }
        }

        private async Task RunComponentImportPipeline()
        {
            string url = _browser.Source?.ToString() ?? "";
            var match = Regex.Match(url, @"C\d+");
            if (!match.Success) return;

            string componentId = match.Value;

            _btnImportLib.Enabled = false;
            _progressBar.Visible = true;
            _lblStatus.Visible = true;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                _lblStatus.Text = "Analyzing Altium project path...";
                string baseDirectory = AltiumApi.GetActiveProjectPath();
                if (baseDirectory.EndsWith("Datasheets")) baseDirectory = Path.GetDirectoryName(baseDirectory) ?? baseDirectory;
                if (string.IsNullOrEmpty(baseDirectory)) baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AltiumEE");

                string libraryFolder = Path.Combine(baseDirectory, "Library");
                if (!Directory.Exists(libraryFolder)) Directory.CreateDirectory(libraryFolder);

                string pcbLibraryPath = Path.Combine(libraryFolder, "EasyEDA.pcblib");
                string schLibraryPath = Path.Combine(libraryFolder, "EasyEDA.schlib");

                var currentDoc = AltiumApi.GlobalVars.Client.GetCurrentView()?.GetOwnerDocument();
                if (currentDoc == null) throw new Exception("No active Altium document found.");

                _lblStatus.Text = $"Downloading EasyEDA data ({componentId})...";
                var cts = new CancellationTokenSource();
                var api = new EasyedaApi();
                var root = await api.GetComponentJsonAsync(componentId, cts.Token);
                if (root?.Component == null) throw new Exception("EasyEDA data not found.");

                var comp = root.Component;
                var ee_footprint = comp.PackageDetail.Footprint;
                var ee_symbol = comp.Symbol;
                string package = ee_footprint.Head.Parameters.Package;

                _lblStatus.Text = $"Generating PCB Footprint: {package}...";
                var pcbDocument = AltiumApi.GlobalVars.Client.OpenDocument("PcbLib", pcbLibraryPath);
                AltiumApi.GlobalVars.Client.ShowDocument(pcbDocument);
                var pcbLib = AltiumApi.GlobalVars.PCBServer.GetCurrentPCBLibrary();

                if (pcbLib.GetComponentByName(package) == null)
                {
                    var model = ee_footprint.GetModel();
                    byte[] rawModelData = model != null ? await api.LoadRawModelAsync(model.Uuid, cts.Token) : null;
                    var libComp = EEPCB.CreateFootprintInLib(package, comp.PackageDetail.Title);
                    AltiumApi.GlobalVars.PCBServer.PreProcess();
                    ee_footprint.AddToComponent(libComp, new EeFootprintContext { Box = ee_footprint.BoundingBox, Layers = ee_footprint.Layers, RawModelTask = Task.FromResult(rawModelData) });
                    AltiumApi.GlobalVars.PCBServer.PostProcess();
                    pcbDocument.DoFileSave("PcbLib");
                }

                _lblStatus.Text = $"Generating SCH Symbol: {ee_symbol.Head.Parameters.Name}...";
                var schDocument = AltiumApi.GlobalVars.Client.OpenDocument("SchLib", schLibraryPath);
                AltiumApi.GlobalVars.Client.ShowDocument(schDocument);
                var productInfo = await api.GetProductInfoAsync(componentId, comp.Owner.Uuid);
                string partName = ee_symbol.Head.Parameters.Name;

                (var schLib, var component) = EESCH.CreateComponentInLib(partName, productInfo?.Description ?? partName, ee_symbol.Head.Parameters.Pre);
                if (schLib != null && component != null)
                {
                    SymbolDrawing.CreateComponent(schLib, component, pcbLibraryPath, package, ee_symbol);
                    if (productInfo?.Parameters != null)
                        foreach (var kvp in productInfo.Parameters) if (!string.IsNullOrEmpty(kvp.Key)) EESCH.AddParameter(component, kvp.Key, kvp.Value ?? "");
                    schDocument.DoFileSave("SchLib");
                }

                _lblStatus.Text = "Finalizing and placing component...";
                AltiumApi.GlobalVars.Client.ShowDocument(currentDoc);
                AltiumApi.GlobalVars.Client.CloseDocument(pcbDocument);
                AltiumApi.GlobalVars.Client.CloseDocument(schDocument);

                var newComponent = AltiumApi.GlobalVars.SCHServer.LoadComponentFromLibrary(partName, schLibraryPath);
                var currentSheet = AltiumApi.GlobalVars.SCHServer.GetCurrentSchDocument();
                currentSheet.AddSchObject(newComponent);
                newComponent.MoveToXY(0, 0);
                currentSheet.GraphicallyInvalidate();

                this.Close();
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                _progressBar.Visible = false;
                _lblStatus.Visible = false;
                _btnImportLib.Enabled = true;
                MessageBox.Show($"Import Error: {ex.Message}", "EasyEDA-LoaderNG", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}