using System;

namespace EasyEDA_Loader
{
    public class EeSymbolPolygon : EeSymbolPolyline
    {
        public static new EeSymbolPolygon FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeSymbolPolygon
            {
                Points = parts[1],
                StrokeColor = parts[2],
                StrokeWidth = parts[3],
                StrokeStyle = parts[4],
                FillColor = parts[5],
                Id = parts[6],
                IsLocked = ParseBoolean(parts[7])
            };
        }
    }

}
