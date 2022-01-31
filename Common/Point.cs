using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bitfish
{
    public class Point
    {
        public float x, y, z;

        public Point() { x = y = z = 0f; }

        public Point(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// Returns the distance between two points
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns>double represeting distance</returns>
        public static double Distance(Point p1, Point p2)
        {
            return Math.Sqrt(
                (p1.x - p2.x) * (p1.x - p2.x) +
                (p1.y - p2.y) * (p1.y - p2.y) +
                (p1.z - p2.z) * (p1.z - p2.z));
        }

        public override string ToString()
        {
            return x + " " + y + " " + z;
        }
    }
}
