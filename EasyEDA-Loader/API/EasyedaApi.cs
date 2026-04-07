using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace EasyEDA_Loader
{
    public class EasyedaApi
    {
        private const string Version = "6.4.19.5";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36";
        private HttpClient HttpClient;

        public EasyedaApi()
        {
            HttpClient = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            })
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            HttpClient.DefaultRequestHeaders.Clear();
            HttpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            HttpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            HttpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        }

        private void LogResponse(string content)
        {
            try
            {
                var jsonObj = JToken.Parse(content);
                string prettyJson = jsonObj.ToString(Formatting.Indented);
                Debug.WriteLine($"[API] Response Content (Pretty JSON):\n{prettyJson}");
                Console.WriteLine($"[API] Response Content (Pretty JSON):\n{prettyJson}");
            }
            catch
            {
                Debug.WriteLine($"[API] Response Content: {content}");
                Console.WriteLine($"[API] Response Content: {content}");
            }
        }

        public async Task<Root> GetComponentJsonAsync(string lcscId, CancellationToken cancellationToken)
        {
            string url = $"https://easyeda.com/api/products/{lcscId}/components?version={Version}";
            Debug.WriteLine($"[API] GET Request: {url}");
            Console.WriteLine($"[API] GET Request: {url}");

            try
            {
                var response = await HttpClient.GetAsync(url, cancellationToken);
                Debug.WriteLine($"[API] Response Status: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"[API] Response Status: {(int)response.StatusCode} {response.StatusCode}");
                
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                
                Debug.WriteLine($"[API] Response Length: {content.Length} characters");
                Console.WriteLine($"[API] Response Length: {content.Length} characters");
                LogResponse(content);
                
                var result = JsonConvert.DeserializeObject<Root>(content);
                Debug.WriteLine($"[API] Deserialized successfully");
                Console.WriteLine($"[API] Deserialized successfully");
                
                return result;
            }
            catch (OperationCanceledException cancel)
            {
                Debug.WriteLine($"[API] Download was cancelled: {cancel.Message}");
                Console.WriteLine($"Download was cancelled: {cancel.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] Error: {ex.Message}");
                Debug.WriteLine($"[API] Stack Trace: {ex.StackTrace}");
                Console.WriteLine($"[API] Error: {ex.Message}");
                Console.WriteLine($"[API] Stack Trace: {ex.StackTrace}");
                throw ex;
            }
        }

        public async Task<BitmapImage> LoadPngAsync(string imageUrl, CancellationToken cancellationToken)
        {
            // Add the host path if it doesnt exist
            if (!imageUrl.Contains("//"))
            {
                imageUrl = "//image.lceda.cn" + imageUrl;
            }

            string fullUrl = $"https:{imageUrl}";
            Debug.WriteLine($"[API] GET Request (Image): {fullUrl}");
            Console.WriteLine($"[API] GET Request (Image): {fullUrl}");

            try
            {
                var res = await HttpClient.GetAsync(fullUrl, cancellationToken);
                Debug.WriteLine($"[API] Response Status: {(int)res.StatusCode} {res.StatusCode}");
                Console.WriteLine($"[API] Response Status: {(int)res.StatusCode} {res.StatusCode}");
                
                res.EnsureSuccessStatusCode();
                var imageData = await res.Content.ReadAsByteArrayAsync();
                
                Debug.WriteLine($"[API] Image Data Length: {imageData.Length} bytes");
                Console.WriteLine($"[API] Image Data Length: {imageData.Length} bytes");

                var bitmap = new BitmapImage();
                var stream = new MemoryStream(imageData);

                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                
                Debug.WriteLine($"[API] Image loaded successfully");
                Console.WriteLine($"[API] Image loaded successfully");
                return bitmap;
            }
            catch (OperationCanceledException cancel)
            {
                Debug.WriteLine($"[API] Download was cancelled: {cancel.Message}");
                Console.WriteLine($"Download was cancelled: {cancel.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] Error loading image: {ex.Message}");
                Console.WriteLine($"[API] Error loading image: {ex.Message}");
                throw ex;
            }
        }

        public async Task<byte[]> LoadModelAsync(string modelUuid, CancellationToken cancellationToken)
        {
            string url = $"https://modules.easyeda.com/qAxj6KHrDKw4blvCG8QJPs7Y/{modelUuid}";
            Debug.WriteLine($"[API] GET Request (Model): {url}");
            Console.WriteLine($"[API] GET Request (Model): {url}");

            try
            {
                var res = await HttpClient.GetAsync(url, cancellationToken);
                Debug.WriteLine($"[API] Response Status: {(int)res.StatusCode} {res.StatusCode}");
                Console.WriteLine($"[API] Response Status: {(int)res.StatusCode} {res.StatusCode}");
                
                res.EnsureSuccessStatusCode();
                var data = await res.Content.ReadAsByteArrayAsync();
                
                Debug.WriteLine($"[API] Model Data Length: {data.Length} bytes");
                Console.WriteLine($"[API] Model Data Length: {data.Length} bytes");
                return data;
            }
            catch (OperationCanceledException cancel)
            {
                Debug.WriteLine($"[API] Download was cancelled: {cancel.Message}");
                Console.WriteLine($"Download was cancelled: {cancel.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] Error loading model: {ex.Message}");
                Console.WriteLine($"[API] Error loading model: {ex.Message}");
                throw ex;
            }
        }

        public async Task<byte[]> LoadRawModelAsync(string modelUuid, CancellationToken cancellationToken)
        {
            string url = $"https://modules.easyeda.com/3dmodel/{modelUuid}";
            Debug.WriteLine($"[API] GET Request (Raw Model): {url}");
            Console.WriteLine($"[API] GET Request (Raw Model): {url}");

            try
            {
                var res = await HttpClient.GetAsync(url, cancellationToken);
                Debug.WriteLine($"[API] Response Status: {(int)res.StatusCode} {res.StatusCode}");
                Console.WriteLine($"[API] Response Status: {(int)res.StatusCode} {res.StatusCode}");
                
                res.EnsureSuccessStatusCode();
                var data = await res.Content.ReadAsByteArrayAsync();
                
                Debug.WriteLine($"[API] Raw Model Data Length: {data.Length} bytes");
                Console.WriteLine($"[API] Raw Model Data Length: {data.Length} bytes");
                return data;
            }
            catch (OperationCanceledException cancel)
            {
                Debug.WriteLine($"[API] Download was cancelled: {cancel.Message}");
                Console.WriteLine($"Download was cancelled: {cancel.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] Error loading raw model: {ex.Message}");
                Console.WriteLine($"[API] Error loading raw model: {ex.Message}");
                throw ex;
            }
        }

        public class PartInfo
        {
            public string Name { get; set; }
            public string Part { get; set; }
            public string Description { get; set; }
            public ProductInfo Info { get; set; }
            public bool HasSymbol { get; set; }
            public bool Has3d { get; set; }
            public bool HasFootprint { get; set; }
        }

        public class ProductInfo
        {
            public string Description { get; set; }
            public Vec3 Size { get; set; }
            public Vec3 Rotation { get; set; }
            public Vec3 Offset { get; set; }
            public Dictionary<string, string> Parameters { get; set; }
        }
        public async Task<ProductInfo> GetProductInfoAsync(string search, string uuid)
        {
            string url = $"https://pro.easyeda.com/api/v2/devices/search";
            Debug.WriteLine($"[API] POST Request: {url}");
            Console.WriteLine($"[API] POST Request: {url}");
            
            var formData = new Dictionary<string, string>
            {
                { "page", "1" },
                { "pageSize", "1" },
                { "uid", uuid },
                { "path", uuid },
                { "wd", search.ToLower() },
                { "returnListStyle", "classifyarr" }
            };
            
            Debug.WriteLine($"[API] Request Data: {string.Join(", ", formData.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            Console.WriteLine($"[API] Request Data: {string.Join(", ", formData.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            
            var content = new FormUrlEncodedContent(formData);
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = content;
            request.Headers.Add("Referer", "https://pro.easyeda.com/editor");
            request.Headers.Add("Origin", "https://pro.easyeda.com");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");

            try
            {
                var response = await HttpClient.SendAsync(request);
                Debug.WriteLine($"[API] Response Status: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"[API] Response Status: {(int)response.StatusCode} {response.StatusCode}");
                
                response.EnsureSuccessStatusCode();
                string result = await response.Content.ReadAsStringAsync();
                
                Debug.WriteLine($"[API] Response Length: {result.Length} characters");
                Console.WriteLine($"[API] Response Length: {result.Length} characters");
                LogResponse(result);
                
                JObject obj = JObject.Parse(result);
                JObject obj_info = (JObject)obj["result"]["lists"]["lcsc"][0];
                var attributes = obj_info["attributes"].ToObject<Dictionary<string, string>>();
                return ProductFromAttributes(attributes, obj_info["description"].ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] Error: {ex.Message}");
                Console.WriteLine($"[API] Error: {ex.Message}");
                return null;
            }
        }

        static public ProductInfo ProductFromAttributes(Dictionary<string, string> attributes, string description)
        {
            Vec3 size = null;
            Vec3 rotation = null;
            Vec3 offset = null;

            if (attributes != null && attributes.ContainsKey("3D Model Transform"))
            {
                try
                {
                    var transformInfo = attributes["3D Model Transform"].Split(',');
                    if (transformInfo.Length >= 9)
                    {
                        size = new Vec3
                        {
                            X = EeShape.ConvertToMM(double.Parse(transformInfo[0])) / 10,
                            Y = EeShape.ConvertToMM(double.Parse(transformInfo[1])) / 10,
                            Z = EeShape.ConvertToMM(double.Parse(transformInfo[2])) / 10,
                        };
                        rotation = new Vec3
                        {
                            X = double.Parse(transformInfo[3]),
                            Y = double.Parse(transformInfo[4]),
                            Z = double.Parse(transformInfo[5]),
                        };
                        offset = new Vec3
                        {
                            X = EeShape.ConvertToMM(double.Parse(transformInfo[6])) / 10,
                            Y = EeShape.ConvertToMM(double.Parse(transformInfo[7])) / 10,
                            Z = EeShape.ConvertToMM(double.Parse(transformInfo[8])) / 10,
                        };
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[API] Error parsing 3D Model Transform: {ex.Message}");
                    Console.WriteLine($"[API] Error parsing 3D Model Transform: {ex.Message}");
                }
            }

            List<string> keysToRemove = new()
            {
                "Add into BOM",
                "Convert to PCB",
                "Symbol",
                "Designator",
                "Footprint",
                "3D Model",
                "3D Model Title",
                "3D Model Transform",
                "Name"
            };
            
            return new ProductInfo
            {
                Description = description,
                Size = size,
                Rotation = rotation,
                Offset = offset,
                Parameters = attributes?.Where(kvp => !keysToRemove.Contains(kvp.Key) && kvp.Value != "-").ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>()
            };
        }

        public async Task<List<PartInfo>> SearchProductInfoAsync(string lcscId)
        {
            string url = $"https://pro.easyeda.com/api/v2/eda/product/search";
            Debug.WriteLine($"[API] POST Request: {url}");
            Console.WriteLine($"[API] POST Request: {url}");

            var payload = new
            {
                codes = lcscId
            };
            
            string jsonPayload = JsonConvert.SerializeObject(payload);
            Debug.WriteLine($"[API] Request Payload: {jsonPayload}");
            Console.WriteLine($"[API] Request Payload: {jsonPayload}");

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = content;
            request.Headers.Add("Referer", "https://pro.easyeda.com/editor");
            request.Headers.Add("Origin", "https://pro.easyeda.com");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            
            try
            {
                var response = await HttpClient.SendAsync(request);
                Debug.WriteLine($"[API] Response Status: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"[API] Response Status: {(int)response.StatusCode} {response.StatusCode}");
                
                response.EnsureSuccessStatusCode();
                string result = await response.Content.ReadAsStringAsync();
                
                Debug.WriteLine($"[API] Response Length: {result.Length} characters");
                Console.WriteLine($"[API] Response Length: {result.Length} characters");
                LogResponse(result);
                
                JObject obj = JObject.Parse(result);
                JArray products = (JArray)obj["result"]["productList"];
                List<PartInfo> productList = new();
                foreach(var product in products)
                {
                    ProductInfo productInfo = new ProductInfo();
                    bool has3d = false;
                    bool hasSymbol = false;
                    bool hasFootprint = false;
                    try
                    {
                        JObject device_info = (JObject)product["device_info"];
                        var attributes = device_info["attributes"].ToObject<Dictionary<string, string>>();
                        productInfo = ProductFromAttributes(attributes, device_info != null ? device_info["Description"].ToString() : "");
                        
                        hasSymbol = device_info["symbol_info"] != null;
                        hasFootprint = device_info["footprint_info"] != null;
                        has3d = device_info["footprint_info"]?["model_3d"] != null;
                    }
                    catch (Exception)
                    {

                    }
                    productList.Add(new PartInfo
                    {
                        Name = product["mpn"].ToString(),
                        Part = product["number"].ToString(),
                        Description = productInfo.Description ?? "",
                        Info = productInfo,
                        HasSymbol = hasSymbol,
                        HasFootprint = hasFootprint,
                        Has3d = has3d,
                    });
                }
                
                Debug.WriteLine($"[API] Found {productList.Count} products");
                Console.WriteLine($"[API] Found {productList.Count} products");
                return productList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] Error: {ex.Message}");
                Console.WriteLine($"[API] Error: {ex.Message}");
                return null;
            }
        }
    }
}
