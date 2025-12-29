using PCB;
using System;
using System.Collections.Generic;

namespace EasyEDA_Loader
{
    public class EeFootprintHole : EeFootprintShape
    {
        public static EeFootprintHole FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeFootprintHole
            {
                CenterX = ConvertToMM(EeShape.ParseDouble(parts[1])),
                CenterY = ConvertToMM(EeShape.ParseDouble(parts[2])),
                Radius = ConvertToMM(EeShape.ParseDouble(parts[3])),
                Id = parts[4],
                IsLocked = ParseBoolean(parts[5]),
            };
        }

        public override List<System.Windows.UIElement> AddToCanvas(
            System.Windows.Controls.Canvas c,
            EeFootprintContext ctx)
        {
            var elements = new List<System.Windows.UIElement>();

            var ellipse = new System.Windows.Shapes.Ellipse
            {
                Width = Radius * 2,
                Height = Radius * 2,
                Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0, 145, 144))
            };

            System.Windows.Controls.Canvas.SetLeft(
                ellipse,
                CenterX - Radius - ctx.Box.X
            );

            System.Windows.Controls.Canvas.SetTop(
                ellipse,
                CenterY - Radius - ctx.Box.Y
            );

            elements.Add(ellipse);
            return elements;
        }

        public override bool AddToComponent(IPCB_LibComponent c, EeFootprintContext ctx)
        {
            var pth = EEPCB.CreatePTH(
                c,
                TLayerConstant.eMultiLayer,
                TExtendedHoleType.eRoundHole,
                TShape.eRounded,
                ConvertX(CenterX, ctx),
                ConvertY(CenterY, ctx),
                Radius * 2,
                Radius * 2,
                Radius * 2,
                "",
                false,
                0
            );

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
