using PCB;
using System;
using System.Collections.Generic;

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
                CenterX = ConvertToMM(EeShape.ParseDouble(parts[2])),
                CenterY = ConvertToMM(EeShape.ParseDouble(parts[3])),
                Width = ConvertToMM(EeShape.ParseDouble(parts[4])),
                Height = ConvertToMM(EeShape.ParseDouble(parts[5])),
                Layer = parts[6],
                Net = parts[7],
                Number = parts[8],
                HoleRadius = ConvertToMM(EeShape.ParseDouble(parts[9])),
                Points = EePoint.ListFromString(parts[10]),
                Rotation = EeShape.ParseDouble(parts[11]),
                Id = parts[12],
                HoleLength = ConvertToMM(EeShape.ParseDouble(parts[13])),
                HolePoints = EePoint.ListFromString(parts[14]),
                IsPlated = ParseBoolean(parts[15]),
                IsLocked = ParseBoolean(parts[16]),
            };
        }

        public override List<System.Windows.UIElement> AddToCanvas(
            System.Windows.Controls.Canvas c,
            EeFootprintContext ctx)
        {
            var elements = new List<System.Windows.UIElement>();

            double radius = 0;
            if (Shape == "OVAL")
            {
                radius = (Width > Height) ? Height / 2 : Width / 2;
            }
            else if (Shape == "ELLIPSE")
            {
                radius = Math.Max(Height / 2, Width / 2);
            }

            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = Width,
                Height = Height,
                RadiusX = radius,
                RadiusY = radius,
                Fill = new System.Windows.Media.SolidColorBrush(
                    ColorHelper.FromHex(ctx.Layers.GetLayerColor(Layer)))
            };

            System.Windows.Controls.Canvas.SetLeft(
                rect,
                CenterX - (Width / 2) - ctx.Box.X
            );
            System.Windows.Controls.Canvas.SetTop(
                rect,
                CenterY - (Height / 2) - ctx.Box.Y
            );

            if (Rotation > 0)
            {
                rect.RenderTransform = new System.Windows.Media.RotateTransform
                {
                    Angle = Rotation,
                    CenterX = Width / 2,
                    CenterY = Height / 2,
                };
            }

            elements.Add(rect);

            // Trou "pathed" (oblong)
            if (HolePoints.Count >= 2)
            {
                var holePath = new System.Windows.Shapes.Path
                {
                    Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0, 145, 144))
                };

                var holeGeometry = new System.Windows.Media.StreamGeometry();

                using (var gctx = holeGeometry.Open())
                {
                    var start = new System.Windows.Point(
                        HolePoints[1].X - ctx.Box.X,
                        HolePoints[1].Y - ctx.Box.Y
                    );
                    var end = new System.Windows.Point(
                        HolePoints[0].X - ctx.Box.X,
                        HolePoints[0].Y - ctx.Box.Y
                    );

                    // Direction
                    var dx = HolePoints[1].X - HolePoints[0].X;
                    var dy = HolePoints[1].Y - HolePoints[0].Y;
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    var ux = dx / len;
                    var uy = dy / len;

                    // Perpendiculaire pour le rayon
                    var rx = -uy * HoleRadius;
                    var ry = ux * HoleRadius;

                    var p1 = new System.Windows.Point(start.X + rx, start.Y + ry);
                    var p2 = new System.Windows.Point(end.X + rx, end.Y + ry);
                    var p3 = new System.Windows.Point(end.X - rx, end.Y - ry);
                    var p4 = new System.Windows.Point(start.X - rx, start.Y - ry);

                    gctx.BeginFigure(p1, isFilled: true, isClosed: true);
                    gctx.LineTo(p2, isStroked: true, isSmoothJoin: false);
                    gctx.ArcTo(
                        p3,
                        new System.Windows.Size(HoleRadius, HoleRadius),
                        0,
                        isLargeArc: false,
                        sweepDirection: System.Windows.Media.SweepDirection.Clockwise,
                        isStroked: true,
                        isSmoothJoin: false
                    );
                    gctx.LineTo(p4, isStroked: true, isSmoothJoin: false);
                    gctx.ArcTo(
                        p1,
                        new System.Windows.Size(HoleRadius, HoleRadius),
                        0,
                        isLargeArc: false,
                        sweepDirection: System.Windows.Media.SweepDirection.Clockwise,
                        isStroked: true,
                        isSmoothJoin: false
                    );
                }

                holePath.Data = holeGeometry;

                if (Rotation > 0)
                {
                    holePath.RenderTransform = new System.Windows.Media.RotateTransform
                    {
                        Angle = Rotation,
                        CenterX = (HolePoints[1].X - ctx.Box.X
                                   - (HolePoints[0].X - ctx.Box.X)) / 2,
                        CenterY = (HolePoints[1].Y - ctx.Box.Y
                                   - (HolePoints[0].Y - ctx.Box.Y)) / 2,
                    };
                }

                elements.Add(holePath);
            }
            else if (HoleRadius > 0) // Trou simple
            {
                var ellipse = new System.Windows.Shapes.Ellipse
                {
                    Width = HoleRadius * 2,
                    Height = HoleRadius * 2,
                    Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0, 145, 144))
                };

                System.Windows.Controls.Canvas.SetLeft(
                    ellipse,
                    CenterX - HoleRadius - ctx.Box.X
                );
                System.Windows.Controls.Canvas.SetTop(
                    ellipse,
                    CenterY - HoleRadius - ctx.Box.Y
                );

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

                    var pad = EEPCB.CreatePTH(
                        c,
                        targetLayer,
                        holeType,
                        padShape,
                        ConvertX(CenterX, ctx),
                        ConvertY(CenterY, ctx),
                        Height,
                        Width,
                        HoleRadius * 2,
                        Number,
                        IsPlated,
                        Rotation
                    );

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
