using PCB;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EasyEDA_Loader
{
    public class EeFootprintCircle : EeFootprintShape
    {
        public static EeFootprintCircle FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeFootprintCircle
            {
                Cx = ConvertToMM(double.Parse(parts[1])),
                Cy = ConvertToMM(double.Parse(parts[2])),
                Radius = ConvertToMM(double.Parse(parts[3])),
                StrokeWidth = ConvertToMM(double.Parse(parts[4])),
                LayerId = parts[5],
                Id = parts[6],
                IsLocked = ParseBoolean(parts[7]),
            };
        }
        public override List<UIElement> AddToCanvas(Canvas c, EeFootprintContext ctx)
        {
            var box = ctx.Box;
            Point startPoint = new Point(Cx - box.X + Radius, Cy - box.Y);
            Point midPoint = new Point(Cx - Radius - box.X, Cy - box.Y);
            Point endPoint = new Point(Cx + Radius - box.X, Cy - box.Y);
            return new List<UIElement>{new Path
                {
                    Stroke = new SolidColorBrush(ColorHelper.FromHex(ctx.Layers.GetLayerColor(LayerId))),
                    StrokeThickness = StrokeWidth,
                    Data = new PathGeometry
                    {
                        Figures = {
                            new PathFigure
                            {
                                StartPoint = startPoint,
                                Segments = new PathSegmentCollection
                                {
                                    new ArcSegment
                                    {
                                        Point = midPoint,
                                        Size = new Size(Radius, Radius),
                                        RotationAngle = 0,
                                        IsLargeArc = false,
                                        SweepDirection = SweepDirection.Clockwise
                                    },
                                    new ArcSegment
                                    {
                                        Point = endPoint,
                                        Size = new Size(Radius, Radius),
                                        RotationAngle = 0,
                                        IsLargeArc = false,
                                        SweepDirection = SweepDirection.Clockwise
                                    }
                                },
                                IsClosed = false
                            }
                        }
                    }
                }
            };
        }
        public override bool AddToComponent(IPCB_LibComponent c, EeFootprintContext ctx)
        {
            TLayerConstant targetLayer;
            var layer = ctx.Layers.GetLayer(LayerId);
            if (layer != null)
            {
                try
                {
                    targetLayer = EEPCB.EELayerToAltium(layer.Name);

                    var arc = EEPCB.CreateArc(c, targetLayer, ConvertX(Cx, ctx), ConvertY(Cy, ctx), Radius, StrokeWidth, 0, 360);
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
            return true;
        }
        public double Cx { get; set; }
        public double Cy { get; set; }
        public double Radius { get; set; }
        public double StrokeWidth { get; set; }
        public string LayerId { get; set; }
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }

}
