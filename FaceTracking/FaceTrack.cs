using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Tracking;
using System;
using System.Drawing;

namespace FaceTracking
{
    public class FaceTrack
    {
        public Guid Id = Guid.NewGuid();

        private Rectangle _location;
        private TrackerKCF _tracker;
        private bool _active = true;

        public bool New = true;

        public Rectangle Location
        {
            get
            {
                return _location;
            }
        }

        public bool Active
        {
            get
            {
                return _active;
            }
        }

        public FaceTrack(Image<Bgr, byte> image, Rectangle location) : base()
        {
            _location = location;
            _tracker = new TrackerKCFWorkaround();

            _tracker.Init(image.Mat, _location);

        }
        public void Update(Image<Bgr, byte> image)
        {
            New = false;

            if (_active)
                _active = _tracker.Update(image.Mat, out _location);
        }
    }
}
