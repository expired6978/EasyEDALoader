using EDP;
using SCH;
using System;
using System.Collections.Generic;
using System.Linq;
using static EasyEDA_Loader.EeSymbolShape;

namespace EasyEDA_Loader
{
    public class AltiumSymbolRectangle
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        public double Width => X2 - X1;
        public double Height => Y2 - Y1;
    }

    public enum PinOrientation
    {
        Top = 0,
        Left,
        Right,
        Bottom
    }

    public class AltiumSymbolPin
    {
        public static TPinElectrical FromEEPinType(EasyedaPinType pinType)
        {
            switch (pinType)
            {
                case EasyedaPinType.Bidirectional:
                    return TPinElectrical.eElectricIO;
                case EasyedaPinType.Input:
                    return TPinElectrical.eElectricInput;
                case EasyedaPinType.Output:
                    return TPinElectrical.eElectricOutput;
                case EasyedaPinType.Power:
                    return TPinElectrical.eElectricPower;
                default:
                    return TPinElectrical.eElectricPassive;
            }
        }

        public static TRotationBy90 FromOrientation(PinOrientation orientation)
        {
            switch (orientation)
            {
                case PinOrientation.Top:
                    return TRotationBy90.eRotate90;
                case PinOrientation.Right:
                    return TRotationBy90.eRotate0;
                case PinOrientation.Left:
                    return TRotationBy90.eRotate180;
                case PinOrientation.Bottom:
                    return TRotationBy90.eRotate270;
                default:
                    return TRotationBy90.eRotate180;
            }
        }

        public double X { get; set; }
        public double Y { get; set; }
        public string Designator { get; set; }
        public string Name { get; set; }
        public TRotationBy90 Orientation { get; set; }
        public double Length { get; set; }
        public TPinElectrical PinType { get; set; }
        public bool ShowName { get; set; }
    }

    public class SymbolDrawing
    {
        static void DistributeEvenly<T>(List<T> source, List<List<T>> targets)
        {
            var counts = targets.Select(l => l.Count).ToList();

            foreach (var item in source)
            {
                int minIndex = 0;
                int minCount = counts[0];

                for (int i = 1; i < counts.Count; i++)
                {
                    if (counts[i] < minCount)
                    {
                        minIndex = i;
                        minCount = counts[i];
                    }
                }

                targets[minIndex].Add(item);
                counts[minIndex]++;
            }
        }

        public static void DrawAltiumRectangle(
            System.Windows.Controls.Canvas c,
            AltiumSymbolRectangle arect)
        {
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = arect.X2 - arect.X1,
                Height = arect.Y2 - arect.Y1,
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
            };

            System.Windows.Controls.Canvas.SetLeft(rect, arect.X1);
            System.Windows.Controls.Canvas.SetTop(rect, arect.Y1);

            c.Children.Add(rect);
        }

        public static void DrawAltiumPin(
            System.Windows.Controls.Canvas c,
            AltiumSymbolPin pin,
            double lineLength)
        {
            double x2 = pin.X;
            double y2 = pin.Y;

            double angle = 0;
            var anchorPoint = new System.Windows.Point(0, 0.5);

            switch (pin.Orientation)
            {
                case TRotationBy90.eRotate90:
                    x2 = pin.X;
                    y2 = pin.Y - lineLength;
                    anchorPoint = new System.Windows.Point(1.0, 0.5);
                    angle = -90;
                    break;

                case TRotationBy90.eRotate180:
                    x2 = pin.X - lineLength;
                    y2 = pin.Y;
                    anchorPoint = new System.Windows.Point(0.0, 0.5);
                    angle = 0;
                    break;

                case TRotationBy90.eRotate0:
                    x2 = pin.X + lineLength;
                    y2 = pin.Y;
                    anchorPoint = new System.Windows.Point(0.0, 0.5);
                    angle = 0;
                    break;

                case TRotationBy90.eRotate270:
                    x2 = pin.X;
                    y2 = pin.Y + lineLength;
                    anchorPoint = new System.Windows.Point(1.0, 0.5);
                    angle = 90;
                    break;
            }

            var line = new System.Windows.Shapes.Line
            {
                X1 = pin.X,
                Y1 = pin.Y,
                X2 = x2,
                Y2 = y2,
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
                StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
                StrokeEndLineCap = System.Windows.Media.PenLineCap.Round
            };

            var label = new System.Windows.Controls.TextBlock
            {
                Text = pin.Designator,
                FontSize = 50,
                Foreground = System.Windows.Media.Brushes.Red,
                RenderTransformOrigin = anchorPoint,
            };

            label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            double textWidth = label.DesiredSize.Width;
            double textHeight = label.DesiredSize.Height;

            double margin = 50;

            if (pin.Orientation != TRotationBy90.eRotate180)
            {
                double angleRadians = angle * Math.PI / 180;

                double offsetX = -Math.Cos(angleRadians) * margin;
                double offsetY = -Math.Sin(angleRadians) * margin;

                System.Windows.Controls.Canvas.SetLeft(label, line.X1 - textWidth + offsetX);
                System.Windows.Controls.Canvas.SetTop(label, line.Y1 - textHeight / 2 + offsetY);
            }
            else
            {
                System.Windows.Controls.Canvas.SetLeft(label, line.X1 + margin);
                System.Windows.Controls.Canvas.SetTop(label, line.Y1 - textHeight / 2);
            }

            label.RenderTransform = new System.Windows.Media.RotateTransform(angle);

            c.Children.Add(label);
            c.Children.Add(line);
        }

        public static (AltiumSymbolRectangle, List<AltiumSymbolPin>) LayoutPins(
            List<EeSymbolShape> Shapes,
            int widthMargin = 8,
            int heightMargin = 8,
            int gridSize = 100)
        {
            List<List<EeSymbolPin>> items = new()
            {
                Shapes.OfType<EeSymbolPin>().Where(shape => shape.Name.Rotation == 270 && shape.Name.TextAnchor == "end").OrderBy(s => s.Settings.PosX).ToList(), // Top
                Shapes.OfType<EeSymbolPin>().Where(shape => shape.Name.Rotation == 0 &&   shape.Name.TextAnchor == "start").OrderBy(s => s.Settings.PosY).ToList(), // Left
                Shapes.OfType<EeSymbolPin>().Where(shape => shape.Name.Rotation == 0 &&   shape.Name.TextAnchor == "end").OrderBy(s => s.Settings.PosY).ToList(),   // Right
                Shapes.OfType<EeSymbolPin>().Where(shape => shape.Name.Rotation == 270 && shape.Name.TextAnchor == "start").OrderBy(s => s.Settings.PosX).ToList(), // Bottom
            };

            var uncategorized = Shapes
                .OfType<EeSymbolPin>()
                .Except(items[0].Union(items[1]).Union(items[2]).Union(items[3]))
                .ToList();

            var populated = items.Where(item => item.Count != 0).OrderBy(item => item.Count).ToList();

            if (populated.Count == 0)
            {
                items[1].AddRange(uncategorized);
            }
            else if (populated.Count == 1)
            {
                populated.First().AddRange(uncategorized);
            }
            else
            {
                DistributeEvenly(uncategorized, items);
            }

            var widthPins = items[0].Count > items[3].Count ? items[0] : items[3];
            var heightPins = items[1].Count > items[2].Count ? items[1] : items[2];

            var halfWidthMargin = widthMargin / 2;
            var halfHeightMargin = heightMargin / 2;

            if (items[0].Count == 0 && items[3].Count == 0)
            {
                heightMargin = 0;
                halfHeightMargin = 0;
            }
            else if (items[1].Count == 0 && items[2].Count == 0)
            {
                widthMargin = 0;
                halfWidthMargin = 0;
            }

            var altiumRect = new AltiumSymbolRectangle
            {
                X1 = 0,
                Y1 = 0,
                X2 = (widthPins.Count + widthMargin) * gridSize + gridSize,
                Y2 = (heightPins.Count + heightMargin) * gridSize + gridSize,
            };

            List<(double x, double y)> offsets = new()
            {
                (halfWidthMargin * gridSize, 0),
                (0,                          halfHeightMargin * gridSize + gridSize),
                (altiumRect.Width,           halfHeightMargin * gridSize + gridSize),
                (halfWidthMargin * gridSize, altiumRect.Height)
            };

            List<AltiumSymbolPin> pins = new();

            for (var i = 0; i < items.Count; ++i)
            {
                double offset_x = offsets[i].x;
                double offset_y = offsets[i].y;

                for (var p = 0; p < items[i].Count; ++p)
                {
                    var x = offset_x;
                    var y = offset_y;

                    switch ((PinOrientation)i)
                    {
                        case PinOrientation.Top:
                        case PinOrientation.Bottom:
                            x += p * gridSize;
                            break;
                        case PinOrientation.Left:
                        case PinOrientation.Right:
                            y += p * gridSize;
                            break;
                    }

                    pins.Add(new AltiumSymbolPin
                    {
                        X = x,
                        Y = y,
                        Orientation = AltiumSymbolPin.FromOrientation((PinOrientation)i),
                        Designator = items[i][p].Settings.SpicePinNumber,
                        Name = items[i][p].Name.Text,
                        Length = 200,
                        ShowName = items[i][p].Name.IsDisplayed,
                        PinType = AltiumSymbolPin.FromEEPinType(items[i][p].Settings.Type)
                    });
                }
            }

            return (altiumRect, pins);
        }

        public static void DrawComponent(
            System.Windows.Controls.Canvas c,
            List<EeSymbolShape> Shapes)
        {
            if (Shapes == null)
                return;

            c.Children.Clear();

            (var rect, var pins) = LayoutPins(Shapes);

            DrawAltiumRectangle(c, rect);

            foreach (var pin in pins)
            {
                DrawAltiumPin(c, pin, 200);
            }
        }

        public static void CreateComponent(
            ISch_Lib schLib,
            ISch_Component component,
            string pcbLibraryPath,
            string package,
            SymbolData ee_symbol)
        {
            (var rect, var pins) = LayoutPins(ee_symbol.Shapes);

            EESCH.CreateRectangle(
                schLib,
                component,
                rect.X1,
                rect.Height - rect.Y1,
                rect.X2,
                rect.Height - rect.Y2);

            foreach (var pin in pins)
            {
                EESCH.CreatePin(
                    schLib,
                    component,
                    pin.X,
                    rect.Height - pin.Y,
                    pin.Designator,
                    pin.Name,
                    pin.Orientation,
                    pin.Length,
                    pin.PinType,
                    pin.ShowName,
                    null);
            }

            EESCH.AssignFootprint(component, pcbLibraryPath, package, "");
            schLib.AddSchComponent(component);
        }
    }
}
