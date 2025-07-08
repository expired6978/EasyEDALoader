using PCB;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

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
        public override List<UIElement> AddToCanvas(Canvas c, EeFootprintContext ctx)
        {
            var box = ctx.Box;
            var elements = new List<UIElement>();
            Ellipse hole = new Ellipse
            {
                Width = Radius * 2,
                Height = Radius * 2,
                Fill = new SolidColorBrush(Color.FromRgb(129, 98, 0)),
            };
            Canvas.SetLeft(hole, CenterX - Radius - box.X);
            Canvas.SetTop(hole, CenterY - Radius - box.Y);

            Ellipse pad = new Ellipse
            {
                Width = Diameter,
                Height = Diameter,
                Fill = new SolidColorBrush(Color.FromRgb(0, 145, 144)),
            };
            Canvas.SetLeft(pad, CenterX - Diameter / 2 - box.X);
            Canvas.SetTop(pad, CenterY - Diameter / 2 - box.Y);

            elements.Add(pad);
            elements.Add(hole);
            return elements;
        }
        public override bool AddToComponent(IPCB_LibComponent c, EeFootprintContext ctx)
        {
            var box = ctx.Box;
            var track = EEPCB.CreateVia(c, TLayerConstant.eTopLayer, TLayerConstant.eBottomLayer, ConvertX(CenterX, ctx), ConvertY(CenterY, ctx), Diameter, Radius * 2);
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
