using DataModel;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;

namespace FaceTracking
{

    public class FaceTrackingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ImageSource Image { get; set; }
        public ImageSource Face { get; set; }

        private string _delay;
        public string Delay
        {
            get
            {
                return _delay;
            }
            set
            {
                _delay = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Delay)));
            }
        }

        private string _rate;
        public string Rate
        {
            get
            {
                return _rate;
            }
            set
            {
                _rate = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rate)));
            }
        }

        private List<IDisposable> _disposables = new List<IDisposable>();

        public FaceTrackingViewModel()
        {
            var frameInterval = int.Parse(ConfigurationManager.AppSettings["FrameInterval"]);

            // Update GUI
            Observable
                .Interval(TimeSpan.FromMilliseconds(frameInterval))
                .Subscribe(_ => {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Face)));
                });

            // Read frames
            var frames = Observable
                .Using(
                    () => new Capture("rtsp://admin:admin@10.15.8.67:554/media/video1", true),
                    capture => Observable
                        .FromEventPattern<EventHandler, EventArgs>(
                            h => capture.ImageGrabbed += h,
                            h => capture.ImageGrabbed -= h)
                        .Sample(TimeSpan.FromMilliseconds(frameInterval))
                        .Sample(TimeSpan.Zero, NewThreadScheduler.Default)
                        .Select(_ => GrabImage(capture))
                        .Where(f => f != null))
                .Catch(Observable.Empty<Frame<Bgr, byte>>())
                .Publish();

            // Track faces
            var detections = Observable
                .Using(
                    () => new FaceTracker(),
                    tracker => frames
                        .Sample(TimeSpan.FromMilliseconds(frameInterval))
                        .Sample(TimeSpan.Zero, NewThreadScheduler.Default)
                        .Select(tracker.Update))
                .Publish();

            // Show delay
            detections
                .Subscribe(x => Application.Current?.Dispatcher.Invoke(() => Delay = $"{DateTime.UtcNow.Subtract(x.Frame.Timestamp).TotalMilliseconds:N0}"));

            // Show 
            detections
                .Buffer(TimeSpan.FromSeconds(1))
                .Select(b => b.Count)
                .Subscribe(c => Application.Current?.Dispatcher.Invoke(() => Rate = $"{c}"));

            // Show detections
            detections
                .Subscribe(x => Application.Current?.Dispatcher.Invoke(() => Image = x.Annotated.ToImageSource()));

            // Show one detection enlarged
            detections
                .Where(x => x.Face != null)
                .Subscribe(i => Application.Current?.Dispatcher.Invoke(() => Face = i.Face.ToImageSource()));

            _disposables.Add(frames.Connect());
            _disposables.Add(detections.Connect());
        }

        private Frame<Bgr, byte> GrabImage(VideoCapture capture)
        {
            try
            {
                var temp = new Image<Bgr, byte>(capture.Width, capture.Height);

                var frame = new Frame<Bgr, byte>()
                {
                    Timestamp = DateTime.UtcNow
                };

                if (capture.Retrieve(temp) && temp.CountNonzero()[0] != 0)
                {
                    frame.Image = temp.Copy();
                        
                    return frame;
                }

                temp.Dispose();
            }
            catch (CvException)
            {

            }

            return null;
        }
    }
}
