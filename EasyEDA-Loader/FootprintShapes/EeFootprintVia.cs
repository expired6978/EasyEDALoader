using PCB;
using System;
using System.Collections.Generic;

namespace EasyEDA_Loader
{
    public class EeFootprintVia : EeFootprintShape
    {
        public static EeFootprintVia FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeFootprintVia
            {
                CenterX = ConvertToMM(double.Parse(parts[1])),
                CenterY = ConvertToMM(double.Parse(parts[2])),
                Diameter = ConvertToMM(double.Parse(parts[3])),
                Net = parts[4],
                Radius = ConvertToMM(double.Parse(parts[5])),
                Id = parts[6],
                IsLocked = ParseBoolean(parts[7]),
            };
        }

        public override List<System.Windows.UIElement> AddToCanvas(
            System.Windows.Controls.Canvas c,
            EeFootprintContext ctx)
        {
            var box = ctx.Box;
            var elements = new List<System.Windows.UIElement>();

            var hole = new System.Windows.Shapes.Ellipse
            {
                Width = Radius * 2,
                Height = Radius * 2,
                Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(129, 98, 0)),
            };
            System.Windows.Controls.Canvas.SetLeft(hole, CenterX - Radius - box.X);
            System.Windows.Controls.Canvas.SetTop(hole, CenterY - Radius - box.Y);

            var pad = new System.Windows.Shapes.Ellipse
            {
                Width = Diameter,
                Height = Diameter,
                Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0, 145, 144)),
            };
            System.Windows.Controls.Canvas.SetLeft(pad, CenterX - Diameter / 2 - box.X);
            System.Windows.Controls.Canvas.SetTop(pad, CenterY - Diameter / 2 - box.Y);

            elements.Add(pad);
            elements.Add(hole);
            return elements;
        }

        public override bool AddToComponent(IPCB_LibComponent c, EeFootprintContext ctx)
        {
            var track = EEPCB.CreateVia(
                c,
                TLayerConstant.eTopLayer,
                TLayerConstant.eBottomLayer,
                ConvertX(CenterX, ctx),
                ConvertY(CenterY, ctx),
                Diameter,
                Radius * 2);

            if (track != null)
            {
                EEPCB.AddToPCB(c, track);
            }
            return true;
        }

        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Diameter { get; set; }
        public string Net { get; set; }
        public double Radius { get; set; }
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }
}
