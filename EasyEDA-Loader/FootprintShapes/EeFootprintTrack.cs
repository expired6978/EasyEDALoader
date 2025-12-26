using PCB;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EasyEDA_Loader
{
    public class EeFootprintTrack : EeFootprintShape
    {
        public static EeFootprintTrack FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeFootprintTrack
            {
                StrokeWidth = ConvertToMM(EeShape.ParseDouble(parts[1])),
                LayerId = parts[2],
                Net = parts[3],
                Points = EePoint.ListFromString(parts[4]),
                Id = parts[5],
                IsLocked = ParseBoolean(parts[6]),
            };
        }
        public override List<UIElement> AddToCanvas(Canvas c, EeFootprintContext ctx)
        {
            var box = ctx.Box;
            var elements = new List<UIElement>();
            for (var i = 1; i < Points.Count; ++i)
            {
                Line line = new Line
                {
                    X1 = Points[i - 1].X - box.X,
                    Y1 = Points[i - 1].Y - box.Y,
                    X2 = Points[i].X - box.X,
                    Y2 = Points[i].Y - box.Y,
                    Stroke = new SolidColorBrush(ColorHelper.FromHex(ctx.Layers.GetLayerColor(LayerId))),
                    StrokeThickness = StrokeWidth,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                elements.Add(line);
            }

            return elements;
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
                    for (var i = 1; i < Points.Count; ++i)
                    {
                        var track = EEPCB.CreateLine(c, targetLayer, ConvertX(Points[i - 1].X, ctx), ConvertY(Points[i - 1].Y, ctx), ConvertX(Points[i].X, ctx), ConvertY(Points[i].Y, ctx), StrokeWidth);
                        if (track != null)
                        {
                            EEPCB.AddToPCB(c, track);
                        }
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
        public double StrokeWidth { get; set; }
        public string LayerId { get; set; }
        public string Net { get; set; }
        List<EePoint> Points { get; set; }
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }

}
