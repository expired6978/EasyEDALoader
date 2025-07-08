using System;
using static System.Math;

namespace EasyEDA_Loader
{
    public static class SvgArcUtils
    {
        public static (double newX, double newY) Rotate(double x, double y, double degrees)
        {
            double radians = (degrees / 180.0) * PI;
            double newX = x * Cos(radians) - y * Sin(radians);
            double newY = x * Sin(radians) + y * Cos(radians);
            return (newX, newY);
        }
        public static (double X, double Y, double Radius, double StartAngle, double EndAngle) ComputeArc(
            double startX,
            double startY,
            double radiusX,
            double radiusY,
            double angleDeg,
            bool largeArcFlag,
            bool sweepFlag,
            double endX,
            double endY)
        {
            // Step 0: Half distance
            double dx2 = (startX - endX) / 2.0;
            double dy2 = (startY - endY) / 2.0;

            // Convert angle to radians
            double angle = ToRadians(angleDeg % 360.0);
            double cosAngle = Cos(angle);
            double sinAngle = Sin(angle);

            // Step 1: Compute (x1, y1)
            double x1 = cosAngle * dx2 + sinAngle * dy2;
            double y1 = -sinAngle * dx2 + cosAngle * dy2;

            // Ensure radii are positive
            radiusX = Abs(radiusX);
            radiusY = Abs(radiusY);
            double PradiusX = radiusX * radiusX;
            double PradiusY = radiusY * radiusY;
            double Px1 = x1 * x1;
            double Py1 = y1 * y1;

            // Adjust radii if needed
            double radiiCheck = (PradiusX != 0 && PradiusY != 0)
                ? (Px1 / PradiusX + Py1 / PradiusY)
                : 0;
            if (radiiCheck > 1)
            {
                double scale = Sqrt(radiiCheck);
                radiusX *= scale;
                radiusY *= scale;
                PradiusX = radiusX * radiusX;
                PradiusY = radiusY * radiusY;
            }

            // Step 2: Compute (cx1, cy1)
            int sign = (largeArcFlag == sweepFlag) ? -1 : 1;
            double sq = 0;
            if ((PradiusX * Py1 + PradiusY * Px1) > 0)
            {
                sq = (PradiusX * PradiusY - PradiusX * Py1 - PradiusY * Px1) /
                     (PradiusX * Py1 + PradiusY * Px1);
            }
            sq = Max(sq, 0);
            double coef = sign * Sqrt(sq);

            double cx1 = coef * ((radiusX * y1) / radiusY);
            double cy1 = (radiusX != 0) ? coef * -((radiusY * x1) / radiusX) : 0;

            // Step 3: Compute center (cx, cy)
            double sx2 = (startX + endX) / 2.0;
            double sy2 = (startY + endY) / 2.0;
            double cx = sx2 + (cosAngle * cx1 - sinAngle * cy1);
            double cy = sy2 + (sinAngle * cx1 + cosAngle * cy1);

            // Compute start and end angles (relative to center)
            double startAngle = Atan2((startY - cy), (startX - cx)) * 180.0 / PI;
            double endAngle = Atan2((endY - cy), (endX - cx)) * 180.0 / PI;

            // Normalize angles to [0, 360)
            if (startAngle < 0) startAngle += 360;
            if (endAngle < 0) endAngle += 360;

            // Enforce sweep direction
            if (sweepFlag && endAngle < startAngle)
                endAngle += 360;
            else if (!sweepFlag && endAngle > startAngle)
                endAngle -= 360;

            // Average radius
            double radius = (radiusX + radiusY) / 2.0;

            return (cx, cy, radius, startAngle, endAngle);
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
