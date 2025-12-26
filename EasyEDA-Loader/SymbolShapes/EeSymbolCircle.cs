using System;

namespace EasyEDA_Loader
{
    public class EeSymbolCircle : EeSymbolShape
    {
        public static EeSymbolCircle FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeSymbolCircle
            {
                CenterX = EeShape.ParseDouble(parts[0]),
                CenterY = EeShape.ParseDouble(parts[1]),
                Radius = EeShape.ParseDouble(parts[2]),
                StrokeColor = parts[3],
                StrokeWidth = parts[4],
                StrokeStyle = parts[5],
                FillColor = parts[6],
                Id = parts[7],
                IsLocked = ParseBoolean(parts[8])
            };
        }

        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Radius { get; set; }
        public string StrokeColor { get; set; }
        public string StrokeWidth { get; set; }
        public string StrokeStyle { get; set; }
        public string FillColor { get; set; }
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }

}
