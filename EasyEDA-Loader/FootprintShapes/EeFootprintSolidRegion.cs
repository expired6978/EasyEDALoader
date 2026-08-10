using PCB;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace EasyEDA_Loader
{
    public class EeFootprintSolidRegion : EeFootprintShape
    {
        public static EeFootprintSolidRegion FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            return new EeFootprintSolidRegion
            {
                LayerId = parts[1],
                PathData = parts[3],
                FillRule = parts[4],
                Id = parts[5],
            };
        }

        // EasyEDA's SOLIDREGION path data is a simple SVG-subset - only "M x y", "L x y" and a
        // closing "Z" appear in every real sample (no curves/arcs within a region's own outline).
        // The command letter is glued directly onto the first coordinate with no space (e.g.
        // "M383.874048 315.27556", not "M 383.874048 ..."). Strips a leading command letter from
        // each token instead of comparing whole tokens (which would never match and silently drop
        // that coordinate), then pairs up every remaining number regardless of which original
        // token it came from.
        public static List<EePoint> ParsePath(string pathData)
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
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
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

        public override bool AddToComponent(IPCB_LibComponent c, EeFootprintContext ctx)
        {
            // "cutout" regions are meant to subtract from the preceding "solid" region (EasyEDA
            // emits them as adjacent pairs sharing the same local coordinates, e.g. a paste-mask
            // pad split into an outer solid block and an inner cutout to leave a border) - not
            // implemented. Rendering a cutout as its own standalone solid shape would be actively
            // wrong (a visible extra blob where there should be a hole), so these are skipped
            // entirely rather than guessed at.
            if (FillRule != "solid")
                return true;

            var layer = ctx.Layers.GetLayer(LayerId);
            if (layer == null)
                return true;

            try
            {
                var targetLayer = EEPCB.EELayerToAltium(layer.Name);

                var rawPoints = ParsePath(PathData);
                if (rawPoints.Count < 3)
                    return true; // not a real polygon

                var localPoints = new List<EePoint>(rawPoints.Count);
                foreach (var p in rawPoints)
                {
                    localPoints.Add(new EePoint { X = ConvertX(ConvertToMM(p.X), ctx), Y = ConvertY(ConvertToMM(p.Y), ctx) });
                }

                var region = EEPCB.CreateSolidRegion(c, targetLayer, localPoints);
                if (region != null)
                {
                    EEPCB.AddToPCB(c, region);
                }
            }
            catch (Exception ex)
            {
                if (ctx.Exception != null && !ctx.Exception(ex))
                {
                    return false;
                }
            }
            return true;
        }

        public string LayerId { get; set; }
        public string PathData { get; set; }
        public string FillRule { get; set; }
        public string Id { get; set; }
    }
}
