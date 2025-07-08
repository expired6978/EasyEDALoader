using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
        private const string UserAgent = "easyeda2kicad v1.0.0"; // Replace with your version
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

            // Set default request headers
            HttpClient.DefaultRequestHeaders.Clear();
            HttpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            HttpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            HttpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        }

        public async Task<Root> GetComponentJsonAsync(string lcscId, CancellationToken cancellationToken)
        {
            string url = $"https://easyeda.com/api/products/{lcscId}/components?version={Version}";

            try
            {
                var response = await HttpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode(); // Throw if not 200 OK
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<Root>(content);
            }
            catch (OperationCanceledException cancel)
            {
                Console.WriteLine($"Download was cancelled: {cancel.Message}");
                return null;
            }
            catch (Exception ex)
            {
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

            try
            {
                var res = await HttpClient.GetAsync($"https:{imageUrl}", cancellationToken);
                res.EnsureSuccessStatusCode();
                var imageData = await res.Content.ReadAsByteArrayAsync();

                var bitmap = new BitmapImage();
                var stream = new MemoryStream(imageData);

                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze(); // Make it cross-thread accessible
                return bitmap;
            }
            catch (OperationCanceledException cancel)
            {
                Console.WriteLine($"Download was cancelled: {cancel.Message}");
                return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<byte[]> LoadModelAsync(string modelUuid, CancellationToken cancellationToken)
        {
            try
            {
                var res = await HttpClient.GetAsync($"https://modules.easyeda.com/qAxj6KHrDKw4blvCG8QJPs7Y/{modelUuid}", cancellationToken);
                res.EnsureSuccessStatusCode();
                return await res.Content.ReadAsByteArrayAsync();
            }
            catch (OperationCanceledException cancel)
            {
                Console.WriteLine($"Download was cancelled: {cancel.Message}");
                return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public async Task<byte[]> LoadRawModelAsync(string modelUuid, CancellationToken cancellationToken)
        {
            try
            {
                var res = await HttpClient.GetAsync($"https://modules.easyeda.com/3dmodel/{modelUuid}", cancellationToken);
                res.EnsureSuccessStatusCode();
                return await res.Content.ReadAsByteArrayAsync();
            }
            catch (OperationCanceledException cancel)
            {
                Console.WriteLine($"Download was cancelled: {cancel.Message}");
                return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public class ProductInfo
        {
            public string Description { get; set; }
            public Vec3 Size { get; set; }
            public Vec3 Rotation { get; set; }
            public Vec3 Offset { get; set; }
            public Dictionary<string, string> Parameters { get; set; }
        }

        public async Task<ProductInfo> GetProductInfoAsync(string lcscId)
        {
            string url = $"https://pro.easyeda.com/api/v2/eda/product/search";

            var payload = new
            {
                codes = lcscId
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            try
            {
                var response = await HttpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode(); // Throw if not 200 OK
                string result = await response.Content.ReadAsStringAsync();
                JObject obj = JObject.Parse(result);
                JObject device_info = (JObject)obj["result"]["productList"][0]["device_info"];
                var attributes = device_info["attributes"].ToObject<Dictionary<string, string>>();
                var transformInfo = attributes["3D Model Transform"].Split(',');
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
                    Description = device_info["Description"].ToString(),
                    Size = new Vec3
                    {
                        X = EeShape.ConvertToMM(double.Parse(transformInfo[0])) / 10,
                        Y = EeShape.ConvertToMM(double.Parse(transformInfo[1])) / 10,
                        Z = EeShape.ConvertToMM(double.Parse(transformInfo[2])) / 10,
                    },
                    Rotation = new Vec3
                    {
                        X = double.Parse(transformInfo[3]),
                        Y = double.Parse(transformInfo[4]),
                        Z = double.Parse(transformInfo[5]),
                    },
                    Offset = new Vec3
                    {
                        X = EeShape.ConvertToMM(double.Parse(transformInfo[6])) / 10,
                        Y = EeShape.ConvertToMM(double.Parse(transformInfo[7])) / 10,
                        Z = EeShape.ConvertToMM(double.Parse(transformInfo[8])) / 10,
                    },
                    Parameters = attributes.Where(kvp => !keysToRemove.Contains(kvp.Key) && kvp.Value != "-").ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                };
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
