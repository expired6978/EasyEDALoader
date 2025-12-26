using PCB;
using System;
using System.Collections.Generic;

// On garde les namespaces WPF, ce sont bien eux qu'on veut ici
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EasyEDA_Loader
{
    public class EeFootprintArc : EeFootprintShape
    {
        public class ArcPath
        {
            public static ArcPath FromString(string path)
            {
                string arcPath = path.Replace(",", " ").Replace("M ", "M").Replace("A ", "A");

                string[] startCoords = arcPath.Split('A')[0].Substring(1).Split(new[] { ' ' }, 2);
                double startX = ConvertToMM(EeShape.ParseDouble(startCoords[0]));
                double startY = ConvertToMM(EeShape.ParseDouble(startCoords[1]));

                string arcParameters = arcPath.Split('A')[1].Replace("  ", " ");
                string[] arcParts = arcParameters.Split(new[] { ' ' }, 7);

                string svgRx = arcParts[0];
                string svgRy = arcParts[1];
                string xAxisRotation = arcParts[2];
                string largeArc = arcParts[3];
                string sweep = arcParts[4];
                string endXStr = arcParts[5];
                string endYStr = arcParts[6];

                var (rx, ry) = SvgArcUtils.Rotate(ConvertToMM(EeShape.ParseDouble(svgRx)), ConvertToMM(EeShape.ParseDouble(svgRy)), 0);

                double endX = ConvertToMM(EeShape.ParseDouble(endXStr));
                double endY = ConvertToMM(EeShape.ParseDouble(endYStr));

                double x, y, radius, startAngle, endAngle;
                if (ry != 0)
                {
                    (x, y, radius, startAngle, endAngle) = SvgArcUtils.ComputeArc(
                        startX, startY, rx, ry,
                        EeShape.ParseDouble(xAxisRotation),
                        largeArc == "1",
                        sweep == "1",
                        endX, endY
                    );
                    return new ArcPath
                    {
                        X = x,
                        Y = y,
                        Radius = radius,
                        StartAngle = startAngle,
                        EndAngle = endAngle,
                        Sweep = sweep == "1"
                            ? System.Windows.Media.SweepDirection.Clockwise
                            : System.Windows.Media.SweepDirection.Counterclockwise,
                    };
                }
                else
                {
                    return null;
                }
            }

            public double X { get; set; }
            public double Y { set; get; }
            public double Radius { get; set; }
            public double StartAngle { get; set; }
            public double EndAngle { get; set; }
            public System.Windows.Media.SweepDirection Sweep { get; set; }
        }

        public static EeFootprintArc FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeFootprintArc
            {
                StrokeWidth = ConvertToMM(EeShape.ParseDouble(parts[1])),
                LayerId = parts[2],
                Net = parts[3],
                Path = ArcPath.FromString(parts[4]),
                HelperDots = parts[5],
                Id = parts[6],
                IsLocked = ParseBoolean(parts[7]),
            };
        }

        public override List<System.Windows.UIElement> AddToCanvas(System.Windows.Controls.Canvas c, EeFootprintContext ctx)
        {
            double startAngleRad = Path.StartAngle * Math.PI / 180.0;
            double endAngleRad = Path.EndAngle * Math.PI / 180.0;

            // Compute start and end points
            System.Windows.Point startPoint = new System.Windows.Point(
                Path.X - ctx.Box.X + Path.Radius * Math.Cos(startAngleRad),
                Path.Y - ctx.Box.Y + Path.Radius * Math.Sin(startAngleRad)
            );

            System.Windows.Point endPoint = new System.Windows.Point(
                Path.X - ctx.Box.X + Path.Radius * Math.Cos(endAngleRad),
                Path.Y - ctx.Box.Y + Path.Radius * Math.Sin(endAngleRad)
            );

            // Determine if the arc is greater than 180°
            bool isLargeArc = Math.Abs(Path.EndAngle - Path.StartAngle) % 360 > 180;

            // Determine sweep direction (WPF)
            System.Windows.Media.SweepDirection sweepDirection =
                (Path.EndAngle - Path.StartAngle) >= 0
                    ? System.Windows.Media.SweepDirection.Clockwise
                    : System.Windows.Media.SweepDirection.Counterclockwise;

            return new List<System.Windows.UIElement>
            {
                new System.Windows.Shapes.Path
                {
                    Stroke = new SolidColorBrush(ColorHelper.FromHex(ctx.Layers.GetLayerColor(LayerId))),
                    StrokeThickness = StrokeWidth,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Data = new PathGeometry
                    {
                        Figures = new PathFigureCollection
                        {
                            new PathFigure
                            {
                                StartPoint = startPoint,
                                Segments = new PathSegmentCollection
                                {
                                    new ArcSegment
                                    {
                                        Point = endPoint,
                                        Size = new System.Windows.Size(Path.Radius, Path.Radius),
                                        RotationAngle = 0,
                                        IsLargeArc = isLargeArc,
                                        SweepDirection = sweepDirection
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

                    var startAngle = Path.Sweep == System.Windows.Media.SweepDirection.Counterclockwise
                        ? ConvertAngle(Path.StartAngle, ctx)
                        : ConvertAngle(Path.EndAngle, ctx);

                    var endAngle = Path.Sweep == System.Windows.Media.SweepDirection.Counterclockwise
                        ? ConvertAngle(Path.EndAngle, ctx)
                        : ConvertAngle(Path.StartAngle, ctx);

                    // Angles are flipped due to coordinate system flip
                    var arc = EEPCB.CreateArc(
                        c,
                        targetLayer,
                        ConvertX(Path.X, ctx),
                        ConvertY(Path.Y, ctx),
                        Path.Radius,
                        StrokeWidth,
                        startAngle,
                        endAngle);

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

        public double StrokeWidth { get; set; }
        public string LayerId { get; set; }
        public string Net { get; set; }
        public ArcPath Path { get; set; }
        public string HelperDots { get; set; }
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }
}
