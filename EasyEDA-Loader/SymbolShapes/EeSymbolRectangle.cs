using System;

namespace EasyEDA_Loader
{
    public class EeSymbolRectangle : EeSymbolShape
    {
        public static EeSymbolRectangle FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeSymbolRectangle
            {
                PosX = EeShape.ParseDouble(parts[1]),
                PosY = EeShape.ParseDouble(parts[2]),
                Rx = ParseNullableDouble(parts[3]),
                Ry = ParseNullableDouble(parts[4]),
                Width = EeShape.ParseDouble(parts[5]),
                Height = EeShape.ParseDouble(parts[6]),
                StrokeColor = parts[7],
                StrokeWidth = parts[8],
                StrokeStyle = parts[9],
                FillColor = parts[10],
                Id = parts[11],
                IsLocked = ParseBoolean(parts[12])
            };
        }
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double? Rx { get; set; }
        public double? Ry { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string StrokeColor { get; set; }
        public string StrokeWidth { get; set; }
        public string StrokeStyle { get; set; }
        public string FillColor { get; set; }
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }

}
