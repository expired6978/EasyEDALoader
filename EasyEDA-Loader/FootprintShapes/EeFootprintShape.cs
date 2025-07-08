using Newtonsoft.Json;
using PCB;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace EasyEDA_Loader
{
    public class EeFootprintContext
    {
        public BoundingBoxMm Box { get; set; }
        public EeFootprintLayers Layers { get; set; }
        public Func<Exception, bool> Exception { get; set; }
        public CancellationToken CancelToken { get; set; }
        public Task<byte[]> ModelTask { get; set; }
        public Task<byte[]> RawModelTask { get; set; }
    }
    [JsonConverter(typeof(EeFootprintShapeConverter))]
    public class EeFootprintShape : EeShape
    {
        public virtual List<UIElement> AddToCanvas(Canvas c, EeFootprintContext context)
        {
            return new List<UIElement>();
        }

        public virtual bool AddToComponent(IPCB_LibComponent c, EeFootprintContext context)
        {
            return true;
        }

        public static double ConvertX(double x, EeFootprintContext ctx)
        {
            return x - ctx.Box.X - (ctx.Box.Width / 2);
        }

        public static double ConvertY(double y, EeFootprintContext ctx)
        {
            return ctx.Box.Height - (y - ctx.Box.Y) - (ctx.Box.Height / 2);
        }

        public static double ConvertAngle(double rot, EeFootprintContext ctx)
        {
            return (360 - rot) % 360;
        }
    }

    public class EeFootprintLayerException : Exception { }

}
