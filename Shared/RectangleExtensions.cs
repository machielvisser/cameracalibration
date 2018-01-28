using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public static class RectangleExtensions
    {
        public static Rectangle Scale(this Rectangle rectangle, float factor)
        {
            return new Rectangle((int)(rectangle.X * factor), (int)(rectangle.Y * factor), (int)(rectangle.Width * factor), (int)(rectangle.Height * factor));
        }

        public static Rectangle Intersection(this Rectangle rectangle, Rectangle other)
        {
            var result = new Rectangle(rectangle.Location, rectangle.Size);
            result.Intersect(other);
            return result;
        }
    }
}
