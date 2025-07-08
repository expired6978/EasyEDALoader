using Newtonsoft.Json;
using System.Collections.Generic;

namespace EasyEDA_Loader
{

    public class SymbolHead
    {
        [JsonProperty("docType")]
        public string DocType { get; set; }

        [JsonProperty("editorVersion")]
        public string EditorVersion { get; set; }

        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("c_para")]
        public SymbolParameters Parameters { get; set; }

        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("puuid")]
        public string Puuid { get; set; }

        [JsonProperty("importFlag")]
        public int ImportFlag { get; set; }

        [JsonProperty("c_spiceCmd")]
        public object CSpiceCmd { get; set; }

        [JsonProperty("hasIdFlag")]
        public bool HasIdFlag { get; set; }

        [JsonProperty("utime")]
        public int Utime { get; set; }

        [JsonProperty("newgId")]
        public bool NewgId { get; set; }

        [JsonProperty("transformList")]
        public string TransformList { get; set; }

        [JsonProperty("uuid_3d")]
        public string Uuid3d { get; set; }
    }
    public class SymbolParameters
    {
        [JsonProperty("pre")]
        public string Pre { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("package")]
        public string Package { get; set; }

        [JsonProperty("Contributor")]
        public string Contributor { get; set; }

        [JsonProperty("Supplier")]
        public string Supplier { get; set; }

        [JsonProperty("Supplier Part")]
        public string SupplierPart { get; set; }

        [JsonProperty("Manufacturer")]
        public string Manufacturer { get; set; }

        [JsonProperty("Manufacturer Part")]
        public string ManufacturerPart { get; set; }

        [JsonProperty("JLCPCB Part Class")]
        public string JLCPCBPartClass { get; set; }
    }
    public class SymbolData
    {
        [JsonProperty("head")]
        public SymbolHead Head { get; set; }

        [JsonProperty("canvas")]
        public string CanvasProperties { get; set; }

        [JsonProperty("shape")]
        [JsonConverter(typeof(EeSymbolShapeListConverter))]
        public List<EeSymbolShape> Shapes { get; set; }

        [JsonProperty("BBox")]
        public BoundingBoxMil BoundingBox { get; set; }
    }

}
