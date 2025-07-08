using PCB;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EasyEDA_Loader
{
    public class EeFootprintPad : EeFootprintShape
    {
        public static EeFootprintPad FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeFootprintPad
            {
                Shape = parts[1],
                CenterX = ConvertToMM(double.Parse(parts[2])),
                CenterY = ConvertToMM(double.Parse(parts[3])),
                Width = ConvertToMM(double.Parse(parts[4])),
                Height = ConvertToMM(double.Parse(parts[5])),
                Layer = parts[6],
                Net = parts[7],
                Number = parts[8],
                HoleRadius = ConvertToMM(double.Parse(parts[9])),
                Points = EePoint.ListFromString(parts[10]),
                Rotation = double.Parse(parts[11]),
                Id = parts[12],
                HoleLength = ConvertToMM(double.Parse(parts[13])),
                HolePoints = EePoint.ListFromString(parts[14]),
                IsPlated = ParseBoolean(parts[15]),
                IsLocked = ParseBoolean(parts[16]),
            };
        }
        public override List<UIElement> AddToCanvas(Canvas c, EeFootprintContext ctx)
        {
            var elements = new List<UIElement>();

            double Radius = 0;
            if (Shape == "OVAL")
            {
                if (Width > Height)
                    Radius = Height / 2;
                else
                    Radius = Width / 2;
            }
            else if (Shape == "ELLIPSE")
            {
                Radius = Math.Max(Height / 2, Width / 2);
            }
            Rectangle rect = new Rectangle
            {
                Width = Width,
                Height = Height,
                RadiusX = Radius,
                RadiusY = Radius,
                Fill = new SolidColorBrush(ColorHelper.FromHex(ctx.Layers.GetLayerColor(Layer))),
            };
            Canvas.SetLeft(rect, CenterX - (Width / 2) - ctx.Box.X);
            Canvas.SetTop(rect, CenterY - (Height / 2) - ctx.Box.Y);

            if (Rotation > 0)
            {
                rect.RenderTransform = new RotateTransform
                {
                    Angle = Rotation,
                    CenterX = Width / 2,
                    CenterY = Height / 2,
                };
            }

            elements.Add(rect);

            if (HolePoints.Count >= 2) // Add a pathed hole
            {
                var holePath = new Path
                {
                    Fill = new SolidColorBrush(Color.FromRgb(0, 145, 144))
                };
                var holeGeometry = new StreamGeometry();

                using (var gctx = holeGeometry.Open())
                {
                    var start = new Point(HolePoints[1].X - ctx.Box.X, HolePoints[1].Y - ctx.Box.Y);
                    var end = new Point(HolePoints[0].X - ctx.Box.X, HolePoints[0].Y - ctx.Box.Y);

                    // Direction vector
                    var dx = HolePoints[1].X - HolePoints[0].X;
                    var dy = HolePoints[1].Y - HolePoints[0].Y;
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    var ux = dx / len;
                    var uy = dy / len;

                    // Perpendicular vector for radius
                    var rx = -uy * HoleRadius;
                    var ry = ux * HoleRadius;

                    // Rounded ends
                    var p1 = new Point(start.X + rx, start.Y + ry);
                    var p2 = new Point(end.X + rx, end.Y + ry);
                    var p3 = new Point(end.X - rx, end.Y - ry);
                    var p4 = new Point(start.X - rx, start.Y - ry);

                    gctx.BeginFigure(p1, true, true);
                    gctx.LineTo(p2, true, false);
                    gctx.ArcTo(p3, new Size(HoleRadius, HoleRadius), 0, false, SweepDirection.Clockwise, true, false);
                    gctx.LineTo(p4, true, false);
                    gctx.ArcTo(p1, new Size(HoleRadius, HoleRadius), 0, false, SweepDirection.Clockwise, true, false);
                }
                holePath.Data = holeGeometry;

                if (Rotation > 0)
                {
                    holePath.RenderTransform = new RotateTransform
                    {
                        Angle = Rotation,
                        CenterX = (HolePoints[1].X - ctx.Box.X - HolePoints[0].X - ctx.Box.X) / 2,
                        CenterY = (HolePoints[1].Y - ctx.Box.Y - HolePoints[0].Y - ctx.Box.Y) / 2,
                    };
                }

                elements.Add(holePath);
            }
            else if (HoleRadius > 0) // Add the single hole if there's a radius
            {
                Ellipse ellipse = new Ellipse
                {
                    Width = HoleRadius * 2,
                    Height = HoleRadius * 2,
                    Fill = new SolidColorBrush(Color.FromRgb(0, 145, 144)),
                };
                Canvas.SetLeft(ellipse, CenterX - HoleRadius - ctx.Box.X);
                Canvas.SetTop(ellipse, CenterY - HoleRadius - ctx.Box.Y);
                elements.Add(ellipse);
            }

            return elements;
        }
        public override bool AddToComponent(IPCB_LibComponent c, EeFootprintContext ctx)
        {
            TLayerConstant targetLayer;
            var layer = ctx.Layers.GetLayer(Layer);
            if (layer != null)
            {
                TShape padShape;
                TExtendedHoleType holeType = TExtendedHoleType.eRoundHole;
                switch (Shape)
                {
                    case "RECT": padShape = TShape.eRectangular; break;
                    case "OVAL": padShape = TShape.eRounded; break;
                    case "ELLIPSE": padShape = TShape.eRounded; break;
                    default: throw new Exception($"Unknown pad shape {Shape}");
                }
                if (HoleLength > 0)
                {
                    holeType = TExtendedHoleType.eSlotHole;
                }

                try
                {
                    targetLayer = EEPCB.EELayerToAltium(layer.Name);
                    var pad = EEPCB.CreatePTH(c, targetLayer, holeType, padShape, ConvertX(CenterX, ctx), ConvertY(CenterY, ctx), Height, Width, HoleRadius * 2, Number, IsPlated, Rotation);
                    if (padShape == TShape.eRounded)
                    {
                        pad.SetState_StackCRPctOnLayer(new V7_Layer(targetLayer), 100);
                    }
                    if (HoleLength > 0)
                    {
                        if (Height > Width)
                        {
                            pad.SetState_HoleRotation(90);
                        }
                        pad.SetState_HoleWidth(AltiumApi.MmToCoord(HoleLength));
                    }
                    EEPCB.AddToPCB(c, pad);
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
        public string Shape { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Layer { get; set; }
        public string Net { get; set; }
        public string Number { get; set; }
        public double HoleRadius { get; set; }
        public List<EePoint> Points { get; set; }
        public double Rotation { get; set; }
        public string Id { get; set; }
        public double HoleLength { get; set; }
        public List<EePoint> HolePoints { get; set; }
        public bool IsPlated { get; set; }
        public bool IsLocked { get; set; }
    }

}
