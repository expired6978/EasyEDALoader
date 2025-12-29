using PCB;
using System;

namespace EasyEDA_Loader
{
    public class EeFootprintText : EeFootprintShape
    {
        public static EeFootprintText FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeFootprintText
            {
                Type = parts[1],
                CenterX = ConvertToMM(EeShape.ParseDouble(parts[2])),
                CenterY = ConvertToMM(EeShape.ParseDouble(parts[3])),
                StrokeWidth = ConvertToMM(EeShape.ParseDouble(parts[4])),
                Rotation = EeShape.ParseDouble(parts[5]),
                Mirror = parts[6],
                LayerId = parts[7],
                Net = parts[8],
                FontSize = ConvertToMM(EeShape.ParseDouble(parts[9])),
                Text = parts[10],
                TextPath = parts[11],
                IsDisplayed = ParseDisplay(parts[12]),
                Id = parts[13],
                IsLocked = ParseBoolean(parts[14]),
            };
        }

        public override bool AddToComponent(IPCB_LibComponent c, EeFootprintContext ctx)
        {
            var box = ctx.Box;
            TLayerConstant targetLayer;
            var layer = ctx.Layers.GetLayer(LayerId);
            if (layer != null)
            {
                try
                {
                    targetLayer = EEPCB.EELayerToAltium(layer.Name);

                    var track = EEPCB.CreateText(c, targetLayer, Text, ConvertX(CenterX, ctx), ConvertY(CenterY, ctx), StrokeWidth, FontSize, Rotation);
                    if (track != null)
                    {
                        EEPCB.AddToPCB(c, track);
                    }
                }
                catch (Exception ex)
                {
                    if (ctx.Exception != null && !ctx.Exception(ex))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public string Type { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double StrokeWidth { get; set; }
        public double Rotation { get; set; }
        public string Mirror { get; set; }
        public string LayerId { get; set; }
        public string Net { get; set; }
        public double FontSize { get; set; }
        public string Text { get; set; }
        public string TextPath { get; set; }
        public bool IsDisplayed { get; set; }
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }

}
