using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;


namespace EasyEDA_Loader
{
    public static class ColorHelper
    {
        public static System.Windows.Media.Color FromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                throw new ArgumentException("Invalid hex string.", nameof(hex));

            // Remove # if present
            hex = hex.TrimStart('#');

            if (hex.Length == 6)
            {
                // Add full alpha if only RGB is given
                hex = "FF" + hex;
            }

            if (hex.Length != 8)
                throw new FormatException("Hex string must be 6 (RRGGBB) or 8 (AARRGGBB) characters long.");

            byte a = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            byte r = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);

            return System.Windows.Media.Color.FromArgb(a, r, g, b);
        }
    }

    public abstract class EeShape
    {
        public static int ParseInt(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            if (int.TryParse(raw, out int num))
            {
                return num;
            }
            return 0;
        }
        public static double ParseFloat(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0.0;
            if (double.TryParse(raw, out double num))
            {
                return num;
            }
            return 0.0;
        }
        public static bool ParseDisplay(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return true;
            switch (raw?.Trim().ToLowerInvariant())
            {
                case "show":
                case "1":
                case "true":
                    return true;

                case "0":
                case "false":
                    return false;

                default:
                    throw new FormatException($"Invalid boolean value: '{raw}'");
            }
        }
        public static bool ParseBoolean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return false;
            if (raw.ToLower()[0] == 'y') return true;
            if (int.TryParse(raw, out int num))
            {
                if (num == 1) return true;
                if (num == 0) return false;
            }

            throw new FormatException($"Invalid numeric boolean value: '{raw}'");
        }

        public static double? ParseNullableDouble(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            if (double.TryParse(raw, out double result))
                return result;

            throw new FormatException($"Invalid double value: '{raw}'");
        }

        public static double MmToDip(double mm)
        {
            return mm * 96.0 / 25.4;
        }

        public static double ConvertToMM(double input)
        {
            return input * 10.0 * 0.0254;
        }
    }
    public class EePoint
    {
        public static List<EePoint> ListFromString(string data)
        {
            var pts = data.Split(' ');
            var points = new List<EePoint>();
            for (var i = 0; i < pts.Length / 2; i++)
            {
                points.Add(new EePoint
                {
                    X = EeShape.ConvertToMM(double.Parse(pts[i * 2])),
                    Y = EeShape.ConvertToMM(double.Parse(pts[i * 2 + 1])),
                });
            }
            return points;
        }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class EeSymbolBbox
    {
        public double X { get; set; }
        public double Y { get; set; }
    }



    public class Vec3
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class EeSymbolShapeListConverter : JsonConverter<List<EeSymbolShape>>
    {
        public override List<EeSymbolShape> ReadJson(JsonReader reader, Type objectType, List<EeSymbolShape> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var result = new List<EeSymbolShape>();

            JArray array = JArray.Load(reader);
            foreach (var token in array)
            {
                string raw = token.ToString();
                if (string.IsNullOrWhiteSpace(raw)) continue;

                EeSymbolShape shape = null;
                switch (raw.Split('~')[0])
                {
                    case "P":
                        shape = EeSymbolPin.FromString(raw);
                        break;
                    case "R":
                        shape = EeSymbolRectangle.FromString(raw);
                        break;
                    case "E":
                        shape = EeSymbolEllipse.FromString(raw);
                        break;
                    case "C":
                        shape = EeSymbolCircle.FromString(raw);
                        break;
                    case "A":
                        shape = EeSymbolArc.FromString(raw);
                        break;
                    case "PL":
                        shape = EeSymbolPolyline.FromString(raw);
                        break;
                    case "PG":
                        shape = EeSymbolPolygon.FromString(raw);
                        break;
                    case "PT":
                        shape = EeSymbolPath.FromString(raw);
                        break;
                }

                if (shape != null)
                    result.Add(shape);
            }

            return result;
        }

        public override void WriteJson(JsonWriter writer, List<EeSymbolShape> value, JsonSerializer serializer)
        {
            throw new NotImplementedException(); // Implement only if you need serialization
        }
    }

    public class MilToMmConverter : JsonConverter<double>
    {
        public override double ReadJson(JsonReader reader, Type objectType, double existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer)
            {
                return EeShape.ConvertToMM(Convert.ToDouble(reader.Value));
            }
            return 0;
        }
        public override void WriteJson(JsonWriter writer, double value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

}
