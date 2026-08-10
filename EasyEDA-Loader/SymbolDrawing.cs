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
        public double RawX { get; set; }
        public double RawY { get; set; }
        public string Designator { get; set; }
        public string Name { get; set; }
        public TRotationBy90 Orientation { get; set; }
        public double Length { get; set; }
        public TPinElectrical PinType { get; set; }
        public bool ShowName { get; set; }
    }

    public class SymbolDrawing
    {
        // Raw space-separated "x y x y ..." pairs, no unit conversion (stays in the same raw
        // mil-like JSON units as BBox/rect throughout this file) - deliberately not reusing
        // EePoint.ListFromString, which converts to mm internally and would mix units.
        static List<EePoint> ParsePointPairs(string data)
        {
            var points = new List<EePoint>();
            var tokens = data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i + 1 < tokens.Length; i += 2)
            {
                if (double.TryParse(tokens[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x)
                    && double.TryParse(tokens[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double y))
                {
                    points.Add(new EePoint { X = x, Y = y });
                }
            }
            return points;
        }

        // Same robust letter-stripping approach as EeFootprintSolidRegion.ParsePath - strips a
        // leading SVG command letter from each token individually rather than comparing whole
        // tokens, so it's correct whether the letter is glued to the first coordinate (as in PCB
        // SOLIDREGION data) or space-separated from it (as seen in this schematic PT/path data,
        // e.g. "M 414 280 L 420 290 L 427 280 Z "). Only M/L/Z seen in any real sample - no curves.
        static List<EePoint> ParseSvgPath(string pathData)
        {
            var numbers = new List<double>();
            var tokens = pathData.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawToken in tokens)
            {
                string token = rawToken;
                if (token.Length > 0 && (token[0] == 'M' || token[0] == 'L' || token[0] == 'Z' || token[0] == 'z'))
                {
                    token = token.Substring(1);
                }
                if (string.IsNullOrEmpty(token))
                    continue;
                if (double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double num))
                {
                    numbers.Add(num);
                }
            }
            var points = new List<EePoint>();
            for (int i = 0; i + 1 < numbers.Count; i += 2)
            {
                points.Add(new EePoint { X = numbers[i], Y = numbers[i + 1] });
            }
            return points;
        }

        // Pins are laid out synthetically (grid position by count/order - see LayoutPins below),
        // completely independent of the JSON's own raw pin coordinates. Graphic shapes (diode
        // arrows, etc.), which ARE only meaningful at their original raw coordinates, can't be
        // drawn 1:1 without landing somewhere unrelated to where the (synthetically repositioned)
        // pins actually ended up. EasyEDA's own graphic coordinates literally coincide with pin
        // raw coordinates in the source data, so calibrating the mapping against the actual pins
        // used for a given axis (rather than the whole bounding box) makes shapes align with
        // those pins exactly, not approximately. Falls back to a bbox-proportional approach
        // per-axis only when there aren't at least 2 pins on that axis with distinct raw
        // positions to calibrate from (e.g. a component with only top/bottom pins has nothing to
        // calibrate Y from).
        //
        // Known limitation: simple 2-point (min/max) calibration per axis, not a full
        // least-squares fit - fine for pins laid out linearly along one axis (which is how
        // LayoutPins always arranges them), but wouldn't average out well for some hypothetical
        // component with non-uniform raw pin spacing on one axis.
        static (double Scale, double Offset) ComputeAxisCalibration(List<double> raw, List<double> synthetic, double bboxMin, double bboxSize, double rectMin, double rectSize)
        {
            double rawMin = double.PositiveInfinity, rawMax = double.NegativeInfinity;
            double synMin = 0, synMax = 0;
            bool found = false;
            for (int i = 0; i < raw.Count; i++)
            {
                if (raw[i] < rawMin) { rawMin = raw[i]; synMin = synthetic[i]; }
                if (raw[i] > rawMax) { rawMax = raw[i]; synMax = synthetic[i]; }
                found = true;
            }
            if (found && rawMax > rawMin)
            {
                double scale = (synMax - synMin) / (rawMax - rawMin);
                double offset = synMin - rawMin * scale;
                return (scale, offset);
            }
            // Fallback: bbox-proportional.
            double bboxScale = bboxSize > 0 ? rectSize / bboxSize : 0;
            double bboxOffset = rectMin - bboxMin * bboxScale;
            return (bboxScale, bboxOffset);
        }

        static EePoint MapToSyntheticRect(EePoint raw, (double Scale, double Offset) calibX, (double Scale, double Offset) calibY)
        {
            return new EePoint { X = raw.X * calibX.Scale + calibX.Offset, Y = raw.Y * calibY.Scale + calibY.Offset };
        }

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
                        RawX = items[i][p].Settings.PosX,
                        RawY = items[i][p].Settings.PosY,
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

        // Draws one part's pins/rectangle/graphics onto the component - does NOT assign
        // footprint or add the component to the library, since for a multi-part component those
        // only happen once, not once per part.
        static public void DrawPart(ISch_Lib schLib, ISch_Component component, SymbolData ee_symbol)
        {
            (var rect, var pins) = SymbolDrawing.LayoutPins(ee_symbol.Shapes);
            EESCH.CreateRectangle(schLib, component, rect.X1, rect.Height - rect.Y1, rect.X2, rect.Height - rect.Y2);
            foreach (var pin in pins)
            {
                EESCH.CreatePin(schLib, component, pin.X, rect.Height - pin.Y, pin.Designator, pin.Name, pin.Orientation, pin.Length, pin.PinType, pin.ShowName, null);
            }

            // Calibrate the graphic-shape mapping against the actual pins (see
            // ComputeAxisCalibration above for why) instead of the whole bounding box. TOP/BOTTOM
            // pins vary in X (Orientation eRotate90/eRotate270), LEFT/RIGHT pins vary in Y
            // (eRotate180/eRotate0).
            var xCalibRaw = new List<double>();
            var xCalibSyn = new List<double>();
            var yCalibRaw = new List<double>();
            var yCalibSyn = new List<double>();
            foreach (var p in pins)
            {
                if (p.Orientation == TRotationBy90.eRotate90 || p.Orientation == TRotationBy90.eRotate270)
                {
                    xCalibRaw.Add(p.RawX);
                    xCalibSyn.Add(p.X);
                }
                else
                {
                    yCalibRaw.Add(p.RawY);
                    yCalibSyn.Add(p.Y);
                }
            }
            var calibX = ComputeAxisCalibration(xCalibRaw, xCalibSyn, ee_symbol.BoundingBox.X, ee_symbol.BoundingBox.Width, rect.X1, rect.Width);
            var calibY = ComputeAxisCalibration(yCalibRaw, yCalibSyn, ee_symbol.BoundingBox.Y, ee_symbol.BoundingBox.Height, rect.Y1, rect.Height);

            // Previously-ignored graphic shapes (diode arrows, etc. - parsed since the start of
            // this project, but never drawn, same gap as PCB SOLIDREGION shapes). See
            // MapToSyntheticRect above for why these go through a scale-to-fit mapping instead of
            // their raw coordinates directly.
            foreach (var shape in ee_symbol.Shapes)
            {
                // Exact-type check, not is/OfType<>() - EeSymbolPolygon inherits EeSymbolPolyline,
                // and a closed/filled polygon must not be drawn as an open polyline outline.
                if (shape.GetType() == typeof(EeSymbolPolyline))
                {
                    var polyline = (EeSymbolPolyline)shape;
                    var rawPts = ParsePointPairs(polyline.Points);
                    var mapped = new List<EePoint>();
                    foreach (var p in rawPts)
                        mapped.Add(MapToSyntheticRect(p, calibX, calibY));
                    for (int i = 1; i < mapped.Count; i++)
                    {
                        EESCH.CreateLine(schLib, component,
                            mapped[i - 1].X, rect.Height - mapped[i - 1].Y,
                            mapped[i].X, rect.Height - mapped[i].Y);
                    }
                }
                else if (shape is EeSymbolPolygon polygon)
                {
                    var rawPts = ParsePointPairs(polygon.Points);
                    var mapped = new List<EePoint>();
                    foreach (var p in rawPts)
                    {
                        var m = MapToSyntheticRect(p, calibX, calibY);
                        mapped.Add(new EePoint { X = m.X, Y = rect.Height - m.Y });
                    }
                    EESCH.CreatePolygon(schLib, component, mapped);
                }
                else if (shape is EeSymbolPath path)
                {
                    var rawPts = ParseSvgPath(path.Paths);
                    var mapped = new List<EePoint>();
                    foreach (var p in rawPts)
                    {
                        var m = MapToSyntheticRect(p, calibX, calibY);
                        mapped.Add(new EePoint { X = m.X, Y = rect.Height - m.Y });
                    }
                    EESCH.CreatePolygon(schLib, component, mapped);
                }
            }
        }

        static public void CreateComponent(ISch_Lib schLib, ISch_Component component, string pcbLibraryPath, string package, SymbolData ee_symbol)
        {
            DrawPart(schLib, component, ee_symbol);
            EESCH.AssignFootprint(component, pcbLibraryPath, package, "");
            schLib.AddSchComponent(component);
        }

        // Real multi-part support. Each subpart becomes a distinct, selectable Altium "part"
        // within the one component (e.g. all 4 gates of a quad op-amp, or STM32MP135's 4 drawn
        // pin groups) sharing one footprint - not just the first subpart's pins.
        // SetState_PartCountNoPart0 (ISch_Component) and SetState_CurrentSchComponentPartId
        // (ISch_Lib) declare the part count / select the part subsequently-created pins/shapes
        // belong to. EESCH.CreatePin/CreateRectangle/CreateLine/CreatePolygon already tag
        // themselves with schLib.GetState_CurrentSchComponentPartId() at creation time
        // (unchanged, pre-existing code) - so setting the current part ID before drawing each
        // subpart is sufficient.
        static public void CreateMultiPartComponent(ISch_Lib schLib, ISch_Component component, string pcbLibraryPath, string package, List<Subpart> subparts)
        {
            component.SetState_PartCountNoPart0(subparts.Count);
            for (int i = 0; i < subparts.Count; i++)
            {
                schLib.SetState_CurrentSchComponentPartId(i + 1); // Altium parts are 1-indexed
                DrawPart(schLib, component, subparts[i].DataStr);
            }
            EESCH.AssignFootprint(component, pcbLibraryPath, package, "");
            schLib.AddSchComponent(component);
        }
    }
}
