using Newtonsoft.Json;
using PCB;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace EasyEDA_Loader
{
    public class EeFootprint3dModel : EeFootprintShape
    {
        public static EeFootprint3dModel FromString(string data)
        {
            var parts = data.Split(new[] { "~" }, StringSplitOptions.None);
            SvgNode node = JsonConvert.DeserializeObject<SvgNode>(parts[1]);
            var rotationParts = node.Attrs.CRotation.Split(new[] { "," }, StringSplitOptions.None);

            // c_origin is wherever the 3D CAD tool's own workspace zero happened to be when the
            // STEP model was authored - it has no relationship to where the model should be
            // placed on the footprint, so it's not used here. The model is placed at the
            // footprint's own local origin instead (the same reference point every other shape
            // is anchored to via ConvertX/ConvertY).

            // Center compute, shouldnt be needed, the GL engine does this for verification of somekind
            /*
                        if(node.Attrs.CEtype == "outline3D")
                        {
                            double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
                            foreach (var childNode in node.ChildNodes)
                            {
                                var points = childNode.Attrs.Points.Split(' ');
                                for (var i = 0; i < points.Length; i += 2)
                                {
                                    minX = Math.Min(double.Parse(points[i], System.Globalization.CultureInfo.InvariantCulture), minX);
                                    minY = Math.Min(double.Parse(points[i + 1], System.Globalization.CultureInfo.InvariantCulture), minY);
                                    maxX = Math.Max(double.Parse(points[i], System.Globalization.CultureInfo.InvariantCulture), maxX);
                                    maxY = Math.Max(double.Parse(points[i + 1], System.Globalization.CultureInfo.InvariantCulture), maxY);
                                }
                            }
                            // Only use computed centers if they were computed
                            if (!double.IsPositiveInfinity(minX) && !double.IsPositiveInfinity(minY) && !double.IsNegativeInfinity(maxX) && !double.IsNegativeInfinity(maxY))
                            {
                                CenterX = ConvertToMM(maxX - (maxX - minX) / 2);
                                CenterY = ConvertToMM(maxY - (maxY - minY) / 2);
                            }
                        }
            */

            return new EeFootprint3dModel
            {
                Name = node.Attrs.Title,
                Uuid = node.Attrs.Uuid,
                Height = ConvertToMM(double.Parse(node.Attrs.CHeight, System.Globalization.CultureInfo.InvariantCulture)),
                Width = ConvertToMM(double.Parse(node.Attrs.CWidth, System.Globalization.CultureInfo.InvariantCulture)),
                Translation = new Vec3
                {
                    X = 0,
                    Y = 0,
                    Z = ConvertToMM(double.Parse(node.Attrs.Z, System.Globalization.CultureInfo.InvariantCulture))
                },
                Rotation = new Vec3
                {
                    X = double.Parse(rotationParts[0], System.Globalization.CultureInfo.InvariantCulture),
                    Y = double.Parse(rotationParts[1], System.Globalization.CultureInfo.InvariantCulture),
                    Z = double.Parse(rotationParts[2], System.Globalization.CultureInfo.InvariantCulture)
                }
            };
        }
        public async Task<double> GetZOffsetFromOrigin(EeFootprintContext ctx)
        {
            double? minZ = null;

            byte[] model = ctx.RawModelTask != null ? await ctx.RawModelTask : await new EasyedaApi().LoadRawModelAsync(Uuid, ctx.CancelToken);

            using var reader = new StringReader(Encoding.UTF8.GetString(model));

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("v ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4 &&
                        double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
                    {
                        if (!minZ.HasValue || z < minZ)
                            minZ = z;
                    }
                }
            }

            if (!minZ.HasValue)
                throw new InvalidDataException("No vertices found in OBJ file.");

            return Math.Abs(minZ.Value);
        }

        public override bool AddToComponent(IPCB_LibComponent c, EeFootprintContext ctx)
        {
            try
            {
                var modelTask = ctx.ModelTask ?? Task.Run(() => new EasyedaApi().LoadModelAsync(Uuid, ctx.CancelToken));
                var heightTask = Task.Run(() => GetZOffsetFromOrigin(ctx));
                Task.WhenAll(modelTask, heightTask).Wait();

                string temp = Path.Combine(Path.GetTempPath(), $"{Uuid}.step");
                File.WriteAllBytes(temp, modelTask.Result);

                // The Z is the lowest Z of the mesh plus the Z offset (hence why we download the Raw mesh and search for the lowest vert.z as this offset is not part of the info)

                // Translation.X/Y are already local-origin coordinates (see FromString) - they land
                // directly on the footprint's own origin without going through ConvertX/ConvertY,
                // which would otherwise apply the bounding-box centering a second time.
                var body = EEPCB.CreateComponentBody(c, temp, Rotation.X, Rotation.Y, Rotation.Z, Translation.X, Translation.Y, Translation.Z + heightTask.Result);
                EEPCB.AddToPCB(c, body);

                File.Delete(temp);
            }
            catch (Exception ex)
            {
                if (ctx.Exception != null && !ctx.Exception(ex))
                    return false;
            }

            return true;
        }

        public string Name { get; set; }
        public string Uuid { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public Vec3 Translation { get; set; }
        public Vec3 Rotation { get; set; }
        public string Raw { get; set; }
        public byte[] Step { get; set; }
    }

}
