using PCB;
using System;
using System.Collections.Generic;

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

        public override List<System.Windows.UIElement> AddToCanvas(
            System.Windows.Controls.Canvas c,
            EeFootprintContext ctx)
        {
            var box = ctx.Box;

            var startPoint = new System.Windows.Point(
                Cx - box.X + Radius,
                Cy - box.Y
            );

            var midPoint = new System.Windows.Point(
                Cx - Radius - box.X,
                Cy - box.Y
            );

            var endPoint = new System.Windows.Point(
                Cx + Radius - box.X,
                Cy - box.Y
            );

            return new List<System.Windows.UIElement>
            {
                new System.Windows.Shapes.Path
                {
                    Stroke = new System.Windows.Media.SolidColorBrush(
                        ColorHelper.FromHex(ctx.Layers.GetLayerColor(LayerId))
                    ),
                    StrokeThickness = StrokeWidth,
                    Data = new System.Windows.Media.PathGeometry
                    {
                        Figures = new System.Windows.Media.PathFigureCollection
                        {
                            new System.Windows.Media.PathFigure
                            {
                                StartPoint = startPoint,
                                Segments = new System.Windows.Media.PathSegmentCollection
                                {
                                    new System.Windows.Media.ArcSegment
                                    {
                                        Point = midPoint,
                                        Size = new System.Windows.Size(Radius, Radius),
                                        RotationAngle = 0,
                                        IsLargeArc = false,
                                        SweepDirection = System.Windows.Media.SweepDirection.Clockwise
                                    },
                                    new System.Windows.Media.ArcSegment
                                    {
                                        Point = endPoint,
                                        Size = new System.Windows.Size(Radius, Radius),
                                        RotationAngle = 0,
                                        IsLargeArc = false,
                                        SweepDirection = System.Windows.Media.SweepDirection.Clockwise
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

                    var arc = EEPCB.CreateArc(
                        c,
                        targetLayer,
                        ConvertX(Cx, ctx),
                        ConvertY(Cy, ctx),
                        Radius,
                        StrokeWidth,
                        0,
                        360
                    );

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
