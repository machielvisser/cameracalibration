using Emgu.CV;
using Emgu.CV.Structure;
using System.Collections.Generic;
using System.Drawing;

namespace DataModel
{
    public class DetectionResult
    {
        public List<Rectangle> Detections;
        public Frame<Bgr, byte> Frame;
        public Image<Bgr, byte> Annotated;
        public Image<Bgr, byte> Face;
    }
}
