using DataModel;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Shared;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ImageSize = System.Drawing.Size;

namespace FaceTracking
{
    public class FaceTracker : IDisposable
    {
        private List<FaceTrack> _tracks = new List<FaceTrack>();
        private Rectangle _imageSize;
        private CascadeClassifier _haarCascade;

        private float _scaleFactor = float.Parse(ConfigurationManager.AppSettings["ScaleFactor"]);
        private float _processingScale = float.Parse(ConfigurationManager.AppSettings["ProcessingScale"]);
        private int _minNeighbours = int.Parse(ConfigurationManager.AppSettings["MinNeighbours"]);
        private int _minFaceSize = int.Parse(ConfigurationManager.AppSettings["MinFaceSize"]);

        public FaceTracker()
        {
            _haarCascade = new CascadeClassifier(ConfigurationManager.AppSettings["Model"]);
        }

        public DetectionResult Update(Frame<Bgr, byte> frame)
        {
            _imageSize = frame.Image.ROI;
            var minFaceSize = (int)(_minFaceSize * _processingScale);

            var pre = frame.Image.Resize(_processingScale, Inter.Linear);

            // Update tracks
            _tracks.ForEach(s => s.Update(pre));

            // Remove stopped tracks
            _tracks.RemoveAll(t => !t.Active);

            // Add new tracks
            _tracks.AddRange(_haarCascade
                .DetectMultiScale(pre, _scaleFactor, _minNeighbours, new ImageSize(minFaceSize, minFaceSize))
                .Where(d => !_tracks.Any(s => s.Location.IntersectsWith(d)))
                .Select(d => new FaceTrack(pre, d)));

            // Annotate frame
            var annotated = pre.Copy();
            _tracks.Select(t => t.Location).ToList().ForEach(d => annotated.Draw(d, new Bgr(Color.Red), 2));
            var detections = _tracks
                    .Select(t => t.Location.Scale(1 / _processingScale).Intersection(_imageSize))
                    .ToList();

            return new DetectionResult
            {
                Frame = frame,
                Annotated = annotated,
                Detections = detections,
                Face = detections.Any() ? frame.Image.GetSubRect(detections.First()) : null
            };
        }

        public void Dispose()
        {

        }
    }
}
