using PCB;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EasyEDA_Loader
{
    public class EeFootprintHole : EeFootprintShape
    {
        public static EeFootprintHole FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeFootprintHole
            {
                CenterX = ConvertToMM(double.Parse(parts[1])),
                CenterY = ConvertToMM(double.Parse(parts[2])),
                Radius = ConvertToMM(double.Parse(parts[3])),
                Id = parts[4],
                IsLocked = ParseBoolean(parts[5]),
            };
        }
        public override List<UIElement> AddToCanvas(Canvas c, EeFootprintContext ctx)
        {
            var elements = new List<UIElement>();
            Ellipse ellipse = new Ellipse
            {
                Width = Radius * 2,
                Height = Radius * 2,
                Fill = new SolidColorBrush(Color.FromRgb(0, 145, 144)),
            };
            Canvas.SetLeft(ellipse, CenterX - Radius - ctx.Box.X);
            Canvas.SetTop(ellipse, CenterY - Radius - ctx.Box.Y);
            elements.Add(ellipse);
            return elements;
        }
        public override bool AddToComponent(IPCB_LibComponent c, EeFootprintContext ctx)
        {
            var pth = EEPCB.CreatePTH(c, TLayerConstant.eMultiLayer, TExtendedHoleType.eRoundHole, TShape.eRounded, ConvertX(CenterX, ctx), ConvertY(CenterY, ctx), Radius * 2, Radius * 2, Radius * 2, "", false, 0);
            if (pth != null)
            {
                // Zero out the expansion on plain holes
                var padCache = pth.GetState_Cache();
                padCache.SolderMaskExpansionValid = TCacheState.eCacheManual;
                padCache.UseSeparateExpansions = true;
                padCache.SolderMaskExpansion = AltiumApi.MmToCoord(0);
                padCache.SolderMaskBottomExpansion = AltiumApi.MmToCoord(0);
                pth.SetState_Cache(padCache);
                EEPCB.AddToPCB(c, pth);
            }
            return true;
        }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Radius { get; set; }
        public string Id { get; set; }
        public bool IsLocked { get; set; }
    }

}
