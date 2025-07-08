using Newtonsoft.Json;
using System.Collections.Generic;

namespace EasyEDA_Loader
{
    public class BoundingBoxMil
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("width")]
        public double Width { get; set; }

        [JsonProperty("height")]
        public double Height { get; set; }
    }

    public class BoundingBoxMm
    {
        [JsonProperty("x")]
        [JsonConverter(typeof(MilToMmConverter))]
        public double X { get; set; }

        [JsonProperty("y")]
        [JsonConverter(typeof(MilToMmConverter))]
        public double Y { get; set; }

        [JsonProperty("width")]
        [JsonConverter(typeof(MilToMmConverter))]
        public double Width { get; set; }

        [JsonProperty("height")]
        [JsonConverter(typeof(MilToMmConverter))]
        public double Height { get; set; }
    }

    public class Lcsc
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("number")]
        public string Number { get; set; }
    }

    public class Owner
    {
        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("nickname")]
        public string Nickname { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }
    }

    public class PackageDetail
    {
        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("docType")]
        public int DocType { get; set; }

        [JsonProperty("updateTime")]
        public int UpdateTime { get; set; }

        [JsonProperty("owner")]
        public Owner Owner { get; set; }

        [JsonProperty("datastrid")]
        public string Datastrid { get; set; }

        [JsonProperty("writable")]
        public bool Writable { get; set; }

        [JsonProperty("dataStr")]
        public FootprintData Footprint { get; set; }
    }

    public class ComponentInfo
    {
        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("docType")]
        public int DocType { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("thumb")]
        public string Thumb { get; set; }

        [JsonProperty("lcsc")]
        public Lcsc Lcsc { get; set; }

        [JsonProperty("szlcsc")]
        public Szlcsc Szlcsc { get; set; }

        [JsonProperty("owner")]
        public Owner Owner { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("updateTime")]
        public int UpdateTime { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }

        [JsonProperty("dataStr")]
        public SymbolData Symbol { get; set; }

        [JsonProperty("datastrid")]
        public string Datastrid { get; set; }

        [JsonProperty("verify")]
        public bool Verify { get; set; }

        [JsonProperty("SMT")]
        public bool SMT { get; set; }

        [JsonProperty("jlcOnSale")]
        public int JlcOnSale { get; set; }

        [JsonProperty("writable")]
        public bool Writable { get; set; }

        [JsonProperty("isFavorite")]
        public bool IsFavorite { get; set; }

        [JsonProperty("packageDetail")]
        public PackageDetail PackageDetail { get; set; }
    }

    public class Root
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("result")]
        public ComponentInfo Component { get; set; }
    }

    public class Szlcsc
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("number")]
        public string Number { get; set; }
    }
    public class Attrs
    {
        [JsonProperty("c_width")]
        public string CWidth { get; set; }

        [JsonProperty("c_height")]
        public string CHeight { get; set; }

        [JsonProperty("c_rotation")]
        public string CRotation { get; set; }

        [JsonProperty("z")]
        public string Z { get; set; }

        [JsonProperty("c_origin")]
        public string COrigin { get; set; }

        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("c_etype")]
        public string CEtype { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("layerid")]
        public string Layerid { get; set; }

        [JsonProperty("transform")]
        public string Transform { get; set; }

        [JsonProperty("fill")]
        public string Fill { get; set; }

        [JsonProperty("c_shapetype")]
        public string CShapetype { get; set; }

        [JsonProperty("points")]
        public string Points { get; set; }
    }

    public class ChildNode
    {
        [JsonProperty("gId")]
        public string GId { get; set; }

        [JsonProperty("nodeName")]
        public string NodeName { get; set; }

        [JsonProperty("nodeType")]
        public int NodeType { get; set; }

        [JsonProperty("attrs")]
        public Attrs Attrs { get; set; }
    }

    public class SvgNode
    {
        [JsonProperty("gId")]
        public string GId { get; set; }

        [JsonProperty("nodeName")]
        public string NodeName { get; set; }

        [JsonProperty("nodeType")]
        public int NodeType { get; set; }

        [JsonProperty("layerid")]
        public string Layerid { get; set; }

        [JsonProperty("attrs")]
        public Attrs Attrs { get; set; }

        [JsonProperty("childNodes")]
        public List<ChildNode> ChildNodes { get; set; }
    }

}
