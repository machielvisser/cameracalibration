using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataModel
{
    public class Detection
    {
        public Rectangle Position;
        public Image<Bgr, byte> Image;
    }
}
