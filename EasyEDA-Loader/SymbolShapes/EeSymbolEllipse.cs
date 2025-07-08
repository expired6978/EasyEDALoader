using System;

namespace EasyEDA_Loader
{
    public class EeSymbolEllipse : EeSymbolShape
    {
        public static EeSymbolEllipse FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeSymbolEllipse
            {
                CenterX = double.Parse(parts[1]),
                CenterY = double.Parse(parts[2]),
                RadiusX = double.Parse(parts[3]),
                RadiusY = double.Parse(parts[4]),
                StrokeColor = parts[5],
                StrokeWidth = parts[6],
                StrokeStyle = parts[7],
                FillColor = parts[8],
                Id = parts[9],
                IsLocked = ParseBoolean(parts[10])
            };
        }

        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double RadiusX { get; set; }
        public double RadiusY { get; set; }
        public string StrokeColor { get; set; }
        public string StrokeWidth { get; set; }
        public string StrokeStyle { get; set; }
        public string FillColor { get; set; }
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }

}
