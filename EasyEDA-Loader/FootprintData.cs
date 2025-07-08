using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PCB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EasyEDA_Loader
{

    public class FootprintParameters
    {
        [JsonProperty("pre")]
        public string Pre { get; set; }

        [JsonProperty("package")]
        public string Package { get; set; }

        [JsonProperty("Contributor")]
        public string Contributor { get; set; }

        [JsonProperty("link")]
        public string Link { get; set; }

        [JsonProperty("3DModel")]
        public string _3DModel { get; set; }
    }

    public class EeFootprintLayer
    {
        public static EeFootprintLayer FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeFootprintLayer
            {
                LayerId = parts[0],
                Name = parts[1],
                Color = parts[2],
                IsDisplayed = EeShape.ParseDisplay(parts[3]),
                Unk2 = EeShape.ParseDisplay(parts[4]),
                Unk3 = EeShape.ParseDisplay(parts[5]),
                Unk4 = EeShape.ParseNullableDouble(parts[6]),
            };
        }
        public string LayerId { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public bool IsDisplayed { get; set; }
        public bool Unk2 { get; set; }
        public bool Unk3 { get; set; }
        public double? Unk4 { get; set; }
    }

    [JsonConverter(typeof(EeFootprintLayersConverter))]
    public class EeFootprintLayers
    {
        public Dictionary<string, EeFootprintLayer> Layers { get; set; }

        public EeFootprintLayer GetLayer(string layer)
        {
            if (Layers.TryGetValue(layer, out EeFootprintLayer value))
            {
                return value;
            }
            return null;
        }

        public string GetLayerColor(string layer)
        {
            if (Layers.TryGetValue(layer, out EeFootprintLayer value))
            {
                return value.Color;
            }
            return null;
        }

        public EeFootprintLayer GetLayerByName(string name)
        {
            foreach (var layer in Layers)
            {
                if (layer.Value.Name == name)
                {
                    return layer.Value;
                }
            }
            return null;
        }
    }

    public class FootprintHead
    {
        [JsonProperty("docType")]
        public string DocType { get; set; }

        [JsonProperty("editorVersion")]
        public string EditorVersion { get; set; }

        [JsonProperty("x")]
        [JsonConverter(typeof(MilToMmConverter))]
        public double X { get; set; }

        [JsonProperty("y")]
        [JsonConverter(typeof(MilToMmConverter))]
        public double Y { get; set; }

        [JsonProperty("c_para")]
        public FootprintParameters Parameters { get; set; }

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
    public class FootprintData
    {
        [JsonProperty("head")]
        public FootprintHead Head { get; set; }
        [JsonProperty("canvas")]
        public string Canvas { get; set; }
        [JsonProperty("shape")]
        public List<EeFootprintShape> Shapes { get; set; }
        [JsonProperty("BBox")]
        public BoundingBoxMm BoundingBox { get; set; }

        [JsonProperty("colors")]
        public List<object> Colors { get; set; }

        [JsonProperty("layers")]
        public EeFootprintLayers Layers { get; set; }

        [JsonProperty("objects")]
        public List<string> Objects { get; set; }

        [JsonProperty("netColors")]
        public List<object> NetColors { get; set; }

        public EeFootprint3dModel GetModel()
        {
            return Shapes.OfType<EeFootprint3dModel>().First();
        }

        public void DrawToCanvas(Canvas c, EeFootprintContext ctx)
        {
            foreach (var shape in Shapes)
            {
                if (shape != null)
                {
                    List<UIElement> elements = shape.AddToCanvas(c, ctx);
                    foreach (var element in elements)
                    {
                        c.Children.Add(element);
                    }
                }
            }
        }

        public void AddToComponent(IPCB_LibComponent c, EeFootprintContext ctx)
        {
            foreach (var shape in Shapes)
            {
                if (shape != null)
                {
                    shape.AddToComponent(c, ctx);
                }
            }
        }
    }

    public class EeFootprintLayersConverter : JsonConverter<EeFootprintLayers>
    {
        public override EeFootprintLayers ReadJson(JsonReader reader, Type objectType, EeFootprintLayers existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var result = new EeFootprintLayers();
            result.Layers = new Dictionary<string, EeFootprintLayer>();

            JArray array = JArray.Load(reader);
            foreach (var token in array)
            {
                string raw = token.ToString();
                EeFootprintLayer layer = EeFootprintLayer.FromString(raw);
                result.Layers.Add(layer.LayerId, layer);
            }

            return result;
        }
        public override void WriteJson(JsonWriter writer, EeFootprintLayers value, JsonSerializer serializer)
        {
            throw new NotImplementedException(); // Implement only if you need serialization
        }
    }


    public class EeFootprintShapeConverter : JsonConverter<EeFootprintShape>
    {
        public override EeFootprintShape ReadJson(JsonReader reader, Type objectType, EeFootprintShape existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                string raw = (string)reader.Value;
                if (string.IsNullOrWhiteSpace(raw)) return null;
                switch (raw.Split('~')[0])
                {
                    case "PAD":
                        return EeFootprintPad.FromString(raw);
                    case "TRACK":
                        return EeFootprintTrack.FromString(raw);
                    case "HOLE":
                        return EeFootprintHole.FromString(raw);
                    case "VIA":
                        return EeFootprintVia.FromString(raw);
                    case "CIRCLE":
                        return EeFootprintCircle.FromString(raw);
                    case "ARC":
                        return EeFootprintArc.FromString(raw);
                    case "RECT":
                        return EeFootprintRectangle.FromString(raw);
                    case "TEXT":
                        return EeFootprintText.FromString(raw);
                    case "SVGNODE":
                        return EeFootprint3dModel.FromString(raw);
                    case "SOLIDREGION":
                        return null;
                }
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, EeFootprintShape value, JsonSerializer serializer)
        {
            throw new NotImplementedException(); // Implement only if you need serialization
        }
    }

}
