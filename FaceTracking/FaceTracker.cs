using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Shared;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Linq;
using ImageSize = System.Drawing.Size;

namespace FaceTracking
{
    public class FaceTracker
    {
        private List<FaceTrack> _tracks = new List<FaceTrack>();
        private Rectangle _imageSize;
        private CascadeClassifier _haarCascade;

        private float _scaleFactor = float.Parse(ConfigurationManager.AppSettings["ScaleFactor"]);
        private float _processingScale = float.Parse(ConfigurationManager.AppSettings["ProcessingScale"]);
        private int _minNeighbours = int.Parse(ConfigurationManager.AppSettings["MinNeighbours"]);
        private int _minFaceSize = int.Parse(ConfigurationManager.AppSettings["MinFaceSize"]);

        public List<Rectangle> Faces
        {
            get
            {
                return _tracks
                    .Select(t => t.Location.Scale(1 / _processingScale).Intersection(_imageSize))
                    .ToList();
            }
        }

        public FaceTracker()
        {
            _haarCascade = new CascadeClassifier(ConfigurationManager.AppSettings["Model"]);
        }

        public FaceTracker Update(Image<Bgr, byte> image)
        {
            _imageSize = image.ROI;

            var pre = image.SmoothMedian(5).Resize(_processingScale, Inter.Linear);

            _tracks.ForEach(s => s.Update(pre));
            _tracks.RemoveAll(t => !t.Active);
            _tracks.AddRange(_haarCascade
                .DetectMultiScale(image.Convert<Gray, byte>().SmoothMedian(7), _scaleFactor, _minNeighbours, new ImageSize(_minFaceSize, _minFaceSize))
                .Select(d => d.Scale(_processingScale))
                .Where(d => !_tracks.Any(s => s.Location.IntersectsWith(d)))
                .Select(d => new FaceTrack(pre, d)));

            return this;
        }
    }
}
