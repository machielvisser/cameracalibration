using Emgu.CV.Tracking;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FaceTracking
{
    public class TrackState
    {
        public TrackerKCF Tracker { get; set; }
        public Rectangle Location { get; set; }
    }
}
