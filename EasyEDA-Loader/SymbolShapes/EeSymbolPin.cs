using System;
using System.Linq;

namespace EasyEDA_Loader
{
    public class EeSymbolPinSettings
    {
        public bool IsDisplayed { get; set; }
        public EeSymbolShape.EasyedaPinType Type { get; set; }
        public string SpicePinNumber { get; set; }
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double Rotation { get; set; }
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }

    public class EeSymbolPinDot
    {
        public double DotX { get; set; }
        public double DotY { get; set; }
    }

    public class EeSymbolPinPath
    {
        public string Path { get; set; }
        public string Color { get; set; }
    }

    public class EeSymbolPinName
    {
        public bool IsDisplayed { get; set; }
        public double PosX { get; set; }
        public double PosY { get; set; }
        public int Rotation { get; set; }
        public string Text { get; set; }
        public string TextAnchor { get; set; }
        public string Font { get; set; }
        public double FontSize { get; set; }
        public string Color { set; get; }

        public static double ParseFontSize(string fontSize)
        {
            if (!string.IsNullOrWhiteSpace(fontSize) && fontSize.Contains("pt"))
            {
                var cleaned = fontSize.Replace("pt", "");
                if (double.TryParse(cleaned, out double result))
                    return result;
            }

            return 7.0f; // default fallback
        }
    }

    public class EeSymbolPinDotBis
    {
        public bool IsDisplayed { get; set; }
        public double CircleX { get; set; }
        public double CircleY { get; set; }
    }

    public class EeSymbolPinClock
    {
        public bool IsDisplayed { get; set; }
        public string Path { get; set; }
    }
    public class EeSymbolPin : EeSymbolShape
    {
        public static EeSymbolPin FromString(string pin_data)
        {
            var segments = pin_data.Split(new[] { "^^" }, StringSplitOptions.None);
            var ee_segments = segments.Select(seg => seg.Split('~')).ToList();

            return new EeSymbolPin
            {
                Settings = new EeSymbolPinSettings
                {
                    IsDisplayed = ParseDisplay(ee_segments[0][1]),
                    Type = (EasyedaPinType)ParseInt(ee_segments[0][2]),
                    SpicePinNumber = ee_segments[0][3],
                    PosX = double.Parse(ee_segments[0][4]),
                    PosY = double.Parse(ee_segments[0][5]),
                    Rotation = ParseInt(ee_segments[0][6]),
                    Id = ee_segments[0][7],
                    IsLocked = ParseBoolean(ee_segments[0][8]),
                },
                PinDot = new EeSymbolPinDot
                {
                    DotX = double.Parse(ee_segments[1][0]),
                    DotY = double.Parse(ee_segments[1][1])
                },
                PinPath = new EeSymbolPinPath
                {
                    Path = ee_segments[2][0],
                    Color = ee_segments[2][1],
                },
                Name = new EeSymbolPinName
                {
                    IsDisplayed = ParseDisplay(ee_segments[3][0]),
                    PosX = double.Parse(ee_segments[3][1]),
                    PosY = double.Parse(ee_segments[3][2]),
                    Rotation = ParseInt(ee_segments[3][3]),
                    Text = ee_segments[3][4],
                    TextAnchor = ee_segments[3][5],
                    Font = ee_segments[3][6],
                    FontSize = EeSymbolPinName.ParseFontSize(ee_segments[3][7]),
                    Color = ee_segments[3][8],
                },
                Designator = new EeSymbolPinName
                {
                    IsDisplayed = ParseDisplay(ee_segments[4][0]),
                    PosX = double.Parse(ee_segments[4][1]),
                    PosY = double.Parse(ee_segments[4][2]),
                    Rotation = ParseInt(ee_segments[4][3]),
                    Text = ee_segments[4][4],
                    TextAnchor = ee_segments[4][5],
                    Font = ee_segments[4][6],
                    FontSize = EeSymbolPinName.ParseFontSize(ee_segments[4][7]),
                    Color = ee_segments[4][8],
                },
                Dot = new EeSymbolPinDotBis
                {
                    IsDisplayed = ParseDisplay(ee_segments[5][0]),
                    CircleX = double.Parse(ee_segments[5][1]),
                    CircleY = double.Parse(ee_segments[5][2]),
                },
                Clock = new EeSymbolPinClock
                {
                    IsDisplayed = ParseDisplay(ee_segments[6][0]),
                    Path = ee_segments[6][1],
                }
            };
        }
        public EeSymbolPinSettings Settings { get; set; }
        public EeSymbolPinDot PinDot { get; set; }
        public EeSymbolPinPath PinPath { get; set; }
        public EeSymbolPinName Name { get; set; }
        public EeSymbolPinName Designator { get; set; }
        public EeSymbolPinDotBis Dot { get; set; }
        public EeSymbolPinClock Clock { get; set; }
    }

}
