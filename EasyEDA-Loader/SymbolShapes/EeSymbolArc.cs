using System;

namespace EasyEDA_Loader
{
    public class EeSymbolArc : EeSymbolShape
    {
        public static EeSymbolArc FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeSymbolArc
            {
                Path = parts[1], // TODO Process SVG path
                HelperDots = parts[2],
                StrokeColor = parts[3],
                StrokeWidth = parts[4],
                StrokeStyle = parts[5],
                FillColor = parts[6],
                Id = parts[7],
                IsLocked = ParseBoolean(parts[8])
            };
        }

        public string Path { get; set; }
        public string HelperDots { get; set; }
        public string StrokeColor { get; set; }
        public string StrokeWidth { get; set; }
        public string StrokeStyle { get; set; }
        public string FillColor { get; set; }
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }

}
