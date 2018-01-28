using DataModel;
using Emgu.CV;
using Emgu.CV.CvEnum;
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

namespace FaceTracking
{

    public class FaceTrackingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private VideoCapture _capture;

        public ImageSource Image { get; set; }
        public ImageSource Face { get; set; }

        public FaceTrackingViewModel()
        {
            var frameInterval = 1000 / float.Parse(ConfigurationManager.AppSettings["FrameRate"]);

            _capture = new VideoCapture(0);

            var size = new Rectangle(0, 0, _capture.Width, _capture.Height);

            Observable
                .Interval(TimeSpan.FromMilliseconds(frameInterval))
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
                .Scan(new FaceTracker(), (t, f) => t.Update(f))
                .Select(t => t.Faces)
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
