using System;

namespace EasyEDA_Loader
{
    public class EeSymbolPath : EeSymbolShape
    {
        public static EeSymbolPath FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeSymbolPath
            {
                Paths = parts[1],
                StrokeColor = parts[2],
                StrokeWidth = parts[3],
                StrokeStyle = parts[4],
                FillColor = parts[5],
                Id = parts[6],
                IsLocked = ParseBoolean(parts[7])
            };
        }

        public string Paths { get; set; }
        public string StrokeColor { get; set; }
        public string StrokeWidth { get; set; }
        public string StrokeStyle { get; set; }
        public string FillColor { get; set; }
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }

}
