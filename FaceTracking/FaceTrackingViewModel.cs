using DataModel;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Tracking;
using Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ImageSize = System.Drawing.Size;

namespace FaceTracking
{

    public class FaceTrackingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private VideoCapture _capture;
        private CascadeClassifier _haarCascade;

        private float _frameInterval = 1000 / float.Parse(ConfigurationManager.AppSettings["FrameRate"]);
        private float _scaleFactor = float.Parse(ConfigurationManager.AppSettings["ScaleFactor"]);
        private int _minNeighbours = int.Parse(ConfigurationManager.AppSettings["MinNeighbours"]);
        private int _minFaceSize = int.Parse(ConfigurationManager.AppSettings["MinFaceSize"]);

        public ImageSource Image { get; set; }
        public ImageSource Face { get; set; }

        public FaceTrackingViewModel()
        {
            _capture = new VideoCapture(0);
            _haarCascade = new CascadeClassifier(ConfigurationManager.AppSettings["Model"]);

            Observable
                .Interval(TimeSpan.FromMilliseconds(_frameInterval))
                .Subscribe(_ => {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Face)));
                });

            var frames = Observable
                .FromEventPattern<EventHandler, EventArgs>(
                    h => _capture.ImageGrabbed += h,
                    h => _capture.ImageGrabbed -= h)
                .Select(_ =>
                {
                    var image = new Image<Bgr, byte>(_capture.Width, _capture.Height);
                    _capture.Retrieve(image);
                    return new Frame<Bgr, byte> { Image = image, Timestamp = DateTime.UtcNow };
                })
                .Publish();

            var detections = frames
                .Select(f => f.Image.Copy())
                .Scan(new List<TrackState>(), (ts, f) =>
                {
                    // Update current tracked faces
                    var updated = ts
                        .Select(s =>
                        {
                            var result = s.Tracker.Update(f.Mat, out Rectangle bbox);
                            bbox.Intersect(f.ROI);
                            s.Location = bbox;

                            if (bbox.IsEmpty)
                            {
                                s.Tracker.Dispose();
                                return null;
                            }
                            else
                                return s;
                        })
                        .Where(s => s != null)
                        .ToList();

                    // Look for new faces
                    updated.AddRange(_haarCascade
                        .DetectMultiScale(f.Convert<Gray, byte>().SmoothMedian(9), _scaleFactor, _minNeighbours, new ImageSize(_minFaceSize, _minFaceSize))
                        .Where(d => !updated.Any(s => s.Location.IntersectsWith(d)))
                        .Select(d => {
                            var trackState = new TrackState { Tracker = new TrackerKCF(), Location = d };
                            trackState.Tracker.Init(f.Mat, d);
                            return trackState;
                        })
                        .ToList());

                    return updated;
                })
                .Select(ts => ts.Select(s => s.Location))
                .Publish();

            detections
                .Zip(frames, (d, f) => new { Detections = d, Image = f.Image.Copy() })
                .Do(x => x.Detections.ToList().ForEach(d => x.Image.Draw(d, new Bgr(System.Drawing.Color.Red), 2)))
                .Subscribe(x => Application.Current?.Dispatcher.Invoke(() => Image = x.Image.ToImageSource()));

            detections
                .Zip(frames, (d, f) => new { Detections = d, Image = f.Image, Timestamp = f.Timestamp })
                .Where(x => x.Detections.Any())
                .Select(x => x.Image.GetSubRect(x.Detections.First()))
                .Subscribe(i => Application.Current?.Dispatcher.Invoke(() => Face = i.ToImageSource()));

            frames.Connect();
            detections.Connect();
            _capture.Start();
        }
    }
}
