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
            var originParts = node.Attrs.COrigin.Split(new[] { "," }, StringSplitOptions.None);
            var rotationParts = node.Attrs.CRotation.Split(new[] { "," }, StringSplitOptions.None);

            double CenterX = EeShape.ParseDouble(originParts[0]);
            double CenterY = EeShape.ParseDouble(originParts[1]);

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
                                    minX = Math.Min(EeShape.ParseDouble(points[i]), minX);
                                    minY = Math.Min(EeShape.ParseDouble(points[i + 1]), minY);
                                    maxX = Math.Max(EeShape.ParseDouble(points[i]), maxX);
                                    maxY = Math.Max(EeShape.ParseDouble(points[i + 1]), maxY);
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
                Height = ConvertToMM(EeShape.ParseDouble(node.Attrs.CHeight)),
                Width = ConvertToMM(EeShape.ParseDouble(node.Attrs.CWidth)),
                Translation = new Vec3
                {
                    X = ConvertToMM(CenterX),
                    Y = ConvertToMM(CenterY),
                    Z = ConvertToMM(EeShape.ParseDouble(node.Attrs.Z))
                },
                Rotation = new Vec3
                {
                    X = EeShape.ParseDouble(rotationParts[0]),
                    Y = EeShape.ParseDouble(rotationParts[1]),
                    Z = EeShape.ParseDouble(rotationParts[2])
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

                // The translation is not quite right, the values shown in "3D Model Manager" are available from the Search API as "3D Model Transform"
                // The Y axis is slightly off and I cannot figure out the missing piece maybe combination of rotation/y-flip/re-center causing this to be wrong
                // Where the mesh starts X,Y in the EE model manager seems to differ from the computed one here
                // The Z is the lowest Z of the mesh plus the Z offset (hence why we download the Raw mesh and search for the lowest vert.z as this offset is not part of the info)

                // Will leave this for now as it's "close enough" most of the time to only need a nudge by a few 10ths of a millimeter
                var body = EEPCB.CreateComponentBody(c, temp, Rotation.X, Rotation.Y, Rotation.Z, ConvertX(Translation.X, ctx), ConvertY(Translation.Y, ctx), Translation.Z + heightTask.Result);
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
