using EDP;
using SCH;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using static EasyEDA_Loader.EeSymbolShape;

namespace EasyEDA_Loader
{
    public class AltiumSymbolRectangle
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        public double Width
        {
            get { return X2 - X1; }
        }
        public double Height
        {
            get { return Y2 - Y1; }
        }
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
        static public TPinElectrical FromEEPinType(EasyedaPinType pinType)
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
        static public TRotationBy90 FromOrientation(PinOrientation orientation)
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
            // Keep track of how many items are in each target list
            var counts = targets.Select(l => l.Count).ToList();

            foreach (var item in source)
            {
                // Find the index of the smallest list
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

                // Add the item to the smallest list
                targets[minIndex].Add(item);
                counts[minIndex]++;
            }
        }


        static public void DrawAltiumRectangle(Canvas c, AltiumSymbolRectangle arect)
        {
            var rect = new Rectangle
            {
                Width = arect.X2 - arect.X1,
                Height = arect.Y2 - arect.Y1,
                Stroke = Brushes.Red,
                StrokeThickness = 2,
            };
            Canvas.SetLeft(rect, arect.X1);
            Canvas.SetTop(rect, arect.Y1);

            c.Children.Add(rect);
        }

        static public void DrawAltiumPin(Canvas c, AltiumSymbolPin pin, double lineLength)
        {
            double x2 = pin.X, y2 = pin.Y;

            double angle = 0;
            Point anchorPoint = new Point(0, 0.5);
            switch (pin.Orientation)
            {
                case TRotationBy90.eRotate90:
                    x2 = pin.X;
                    y2 = pin.Y - lineLength;
                    anchorPoint = new Point(1.0, 0.5);
                    angle = -90;
                    break;
                case TRotationBy90.eRotate180:
                    x2 = pin.X - lineLength;
                    y2 = pin.Y;
                    anchorPoint = new Point(0.0, 0.5);
                    angle = 0;
                    break;
                case TRotationBy90.eRotate0:
                    x2 = pin.X + lineLength;
                    y2 = pin.Y;
                    anchorPoint = new Point(0.0, 0.5);
                    angle = 0;
                    break;
                case TRotationBy90.eRotate270:
                    x2 = pin.X;
                    y2 = pin.Y + lineLength;
                    anchorPoint = new Point(1.0, 0.5);
                    angle = 90;
                    break;
                default:
                    break;
            }

            var line = new Line
            {
                X1 = pin.X,
                Y1 = pin.Y,
                X2 = x2,
                Y2 = y2,
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            TextBlock label = new TextBlock
            {
                Text = pin.Designator,
                FontSize = 50,
                Foreground = Brushes.Red,
                RenderTransformOrigin = anchorPoint,
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double textWidth = label.DesiredSize.Width;
            double textHeight = label.DesiredSize.Height;

            double margin = 50; // distance in pixels
            if (pin.Orientation != TRotationBy90.eRotate180)
            {

                double angleRadians = angle * Math.PI / 180;

                // Offset text away from line start, along line direction
                double offsetX = -Math.Cos(angleRadians) * margin;
                double offsetY = -Math.Sin(angleRadians) * margin;

                Canvas.SetLeft(label, line.X1 - textWidth + offsetX);
                Canvas.SetTop(label, line.Y1 - textHeight / 2 + offsetY);
            }
            else
            {
                Canvas.SetLeft(label, line.X1 + margin);
                Canvas.SetTop(label, line.Y1 - textHeight / 2);
            }


            label.RenderTransform = new RotateTransform(angle);
            c.Children.Add(label);

            c.Children.Add(line);
        }

        static public (AltiumSymbolRectangle, List<AltiumSymbolPin>) LayoutPins(List<EeSymbolShape> Shapes, int widthMargin = 8, int heightMargin = 8, int gridSize = 100)
        {
            List<List<EeSymbolPin>> items = new()
            {
                // Top
                Shapes.OfType<EeSymbolPin>().Where(shape => shape.Name.Rotation == 270 && shape.Name.TextAnchor == "end").OrderBy(s => s.Settings.PosX).ToList(),
                // Left
                Shapes.OfType<EeSymbolPin>().Where(shape => shape.Name.Rotation == 0 && shape.Name.TextAnchor == "start").OrderBy(s => s.Settings.PosY).ToList(),
                // Right
                Shapes.OfType<EeSymbolPin>().Where(shape => shape.Name.Rotation == 0 && shape.Name.TextAnchor == "end").OrderBy(s => s.Settings.PosY).ToList(),
                // Bottom
                Shapes.OfType<EeSymbolPin>().Where(shape => shape.Name.Rotation == 270 && shape.Name.TextAnchor == "start").OrderBy(s => s.Settings.PosX).ToList()
            };

            // If there were uncategorized pins, put them somewhere
            var uncategorized = Shapes.OfType<EeSymbolPin>().Except(items[0].Union(items[1]).Union(items[2]).Union(items[3])).ToList();

            var populated = items.Where(item => item.Count != 0).OrderBy(item => item.Count).ToList();
            if (populated.Count == 0) // Everything was uncategorized? Weird, add everything to the left
            {
                items[1].AddRange(uncategorized);
            }
            else if (populated.Count == 1) // If there's only one direction, just add everything to it
            {
                populated.FirstOrDefault().AddRange(uncategorized);
            }
            else // There are multiple available directions, distribute them starting with the least populated
            {
                DistributeEvenly(uncategorized, items);
            }

            // Select the largest of the two sides, these will determine the dimensions of the encompassing rect
            var widthPins = items[0].Count > items[3].Count ? items[0] : items[3];
            var heightPins = items[1].Count > items[2].Count ? items[1] : items[2];

            var halfWidthMargin = widthMargin / 2;
            var halfHeightMargin = heightMargin / 2;

            if (items[0].Count == 0 && items[3].Count == 0) // Only Left/Right
            {
                heightMargin = 0;
                halfHeightMargin = heightMargin / 2;
            }
            else if (items[1].Count == 0 && items[2].Count == 0) // Only Top/Bottom
            {
                widthMargin = 0;
                halfWidthMargin = widthMargin / 2;
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
                (0, halfHeightMargin * gridSize + gridSize),
                (altiumRect.Width, halfHeightMargin * gridSize + gridSize),
                (halfWidthMargin * gridSize, altiumRect.Height)
            };

            List<AltiumSymbolPin> pins = new();
            for (var i = 0; i < items.Count; ++i)
            {
                double offset_x = offsets[i].x, offset_y = offsets[i].y;
                for (var p = 0; p < items[i].Count; ++p)
                {
                    var x = offset_x;
                    var y = offset_y;
                    switch ((PinOrientation)i)
                    {
                        case PinOrientation.Top:
                            x += p * gridSize;
                            break;
                        case PinOrientation.Left:
                            y += p * gridSize;
                            break;
                        case PinOrientation.Right:
                            y += p * gridSize;
                            break;
                        case PinOrientation.Bottom:
                            x += p * gridSize;
                            break;
                        default:
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

        static public void DrawComponent(Canvas c, List<EeSymbolShape> Shapes)
        {
            if (Shapes != null)
            {
                c.Children.Clear();

                (var rect, var pins) = LayoutPins(Shapes);

                DrawAltiumRectangle(c, rect);
                foreach (var pin in pins)
                {
                    DrawAltiumPin(c, pin, 200);
                }

            }
        }

        static public void CreateComponent(ISch_Lib schLib, ISch_Component component, string pcbLibraryPath, string package, SymbolData ee_symbol)
        {
            (var rect, var pins) = SymbolDrawing.LayoutPins(ee_symbol.Shapes);
            EESCH.CreateRectangle(schLib, component, rect.X1, rect.Height - rect.Y1, rect.X2, rect.Height - rect.Y2);
            foreach (var pin in pins)
            {
                EESCH.CreatePin(schLib, component, pin.X, rect.Height - pin.Y, pin.Designator, pin.Name, pin.Orientation, pin.Length, pin.PinType, pin.ShowName, null);
            }
            EESCH.AssignFootprint(component, pcbLibraryPath, package, "");
            schLib.AddSchComponent(component);
        }
    }
}
