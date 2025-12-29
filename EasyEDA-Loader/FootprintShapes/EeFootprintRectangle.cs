using PCB;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EasyEDA_Loader
{
    public class EeFootprintRectangle : EeFootprintShape
    {
        public static EeFootprintRectangle FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeFootprintRectangle
            {
                X = ConvertToMM(EeShape.ParseDouble(parts[1])),
                Y = ConvertToMM(EeShape.ParseDouble(parts[2])),
                Width = ConvertToMM(EeShape.ParseDouble(parts[3])),
                Height = ConvertToMM(EeShape.ParseDouble(parts[4])),
                StrokeWidth = ConvertToMM(EeShape.ParseDouble(parts[5])),
                Id = parts[6],
                LayerId = parts[7],
                IsLocked = ParseBoolean(parts[8]),
            };
        }
        public override List<UIElement> AddToCanvas(Canvas c, EeFootprintContext ctx)
        {
            var box = ctx.Box;
            var Stroke = new SolidColorBrush(ColorHelper.FromHex(ctx.Layers.GetLayerColor(LayerId)));
            var segments = new List<Tuple<double, double, double, double>>
            {
                Tuple.Create(X - box.X, Y - box.Y, X + Width - box.X, Y - box.Y ),
                Tuple.Create(X - box.X, Y - box.Y, X - box.X, Y + Height - box.Y ),
                Tuple.Create(X - box.X, Height - box.Y, X + Width - box.X, Y + Height - box.Y ),
                Tuple.Create(X + Width - box.X, Y - box.Y, X + Width - box.X, Y + Height - box.Y )
            };

            var elements = new List<UIElement>();
            foreach (var seg in segments)
            {
                elements.Add(new Line
                {
                    X1 = seg.Item1,
                    Y1 = seg.Item2,
                    X2 = seg.Item3,
                    Y2 = seg.Item4,
                    Stroke = Stroke,
                    StrokeThickness = StrokeWidth,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                });
            }
            return elements;
        }
        public override bool AddToComponent(IPCB_LibComponent c, EeFootprintContext ctx)
        {
            var box = ctx.Box;
            var segments = new List<Tuple<double, double, double, double>>
            {
                Tuple.Create(X, Y, X + Width, Y ),
                Tuple.Create(X, Y, X, Y + Height ),
                Tuple.Create(X, Height, X + Width, Y + Height ),
                Tuple.Create(X + Width, Y, X + Width, Y + Height )
            };

            TLayerConstant targetLayer;
            var layer = ctx.Layers.GetLayer(LayerId);
            if (layer != null)
            {
                foreach (var seg in segments)
                {
                    try
                    {
                        targetLayer = EEPCB.EELayerToAltium(layer.Name);

                        var arc = EEPCB.CreateLine(c, targetLayer, ConvertX(seg.Item1, ctx), ConvertY(seg.Item2, ctx), ConvertX(seg.Item3, ctx), ConvertY(seg.Item4, ctx), StrokeWidth);
                        if (arc != null)
                        {
                            EEPCB.AddToPCB(c, arc);
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
            }
            return true;
        }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double StrokeWidth { get; set; }
        public string Id { get; set; }
        public string LayerId { get; set; }
        public bool IsLocked { get; set; }
    }

}
