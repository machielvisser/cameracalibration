using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataModel
{
    public class Pattern
    {
        public VectorOfPointF Corners;
        public Image<Bgr, byte> Image;
        public bool Found;
    }
}
