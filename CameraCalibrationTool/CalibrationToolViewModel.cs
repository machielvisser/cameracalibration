using DataModel;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Media;
using ImageSize = System.Drawing.Size;

namespace CameraCalibrationTool
{
    public class CalibrationToolViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        
        private const int _nCornersHorizontal = 9;
        private const int _nCornersVertical = 6;
        private const float _squareSize = 0.034F;
        
        private const int _sampleRate = 2;

        private const int _frameInterval = 50;

        private const int _numberOfPatterns = 30;

        private MCvPoint3D32f _realLocation;

        private string _source = "rtsp://admin:admin@10.15.8.67:554/media/video1";
        public string Source
        {
            get
            {
                return _source;
            }
            set
            {
                _source = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Source)));
            }
        }

        private ImageSource _step1;
        public ImageSource Step1
        {
            get
            {
                return _step1;
            }
            set
            {
                _step1 = value;
            }
        }

        private ImageSource _step2;
        public ImageSource Step2
        {
            get
            {
                return _step2;
            }
            set
            {
                _step2 = value;
            }
        }

        private int _patternQuality;
        public int PatternQuality
        {
            get
            {
                return _patternQuality;
            }
            set
            {
                _patternQuality = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PatternQuality)));
            }
        }

        private double _faceAngle;
        public double FaceAngle
        {
            get
            {
                return _faceAngle;
            }
            set
            {
                _faceAngle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FaceAngle)));
            }
        }

        private Pattern[] _patternSet;
        public Pattern[] PatternsSet
        {
            get
            {
                return _patternSet;
            }
            set
            {
                _patternSet = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PatternsSet)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PatternsAvailable)));
            }
        }

        public bool PatternsAvailable
        {
            get
            {
                return _patternSet?.Length == _numberOfPatterns;
            }
        }

        private Calibration _calibration;
        public Calibration Calibration
        {
            get
            {
                return _calibration;
            }
            set
            {
                _calibration = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Calibration)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CalibrationAvailable)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntrinsicAvailable)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CalibrationError)));
            }
        }

        public bool IntrinsicAvailable
        {
            get
            {
                return _calibration?.RotationVectors != null;
            }
        }

        public bool CalibrationAvailable
        {
            get
            {
                return _calibration != null;
            }
        }

        public double CalibrationError
        {
            get
            {
                return _calibration.Error;
            }
        }


        private ISubject<string> _saveTriggers = new Subject<string>();
        private ISubject<Calibration> _calibrations = new Subject<Calibration>();
        private ISubject<bool> _enable = new Subject<bool>();
        private IConnectableObservable<Image<Bgr, byte>> _images;
        private ISubject<Pattern> _patterns = new Subject<Pattern>();

        private AsyncSubject<MCvPoint3D32f[]> _realCorners;

        public CalibrationToolViewModel()
        {
            _realLocation = new MCvPoint3D32f
            {
                X = 0.424f,
                Y = 0,
                Z = 1.965f
            };

            Observable
                .Interval(TimeSpan.FromMilliseconds(50))
                .Subscribe(_ => {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Step1)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Step2)));
                });

            _realCorners = Observable
                .Range(0, _nCornersVertical)
                .SelectMany(y => Observable
                    .Range(0, _nCornersHorizontal)
                    .Select(x => new MCvPoint3D32f(x * _squareSize, y * _squareSize, 0.0F))
                    .ToArray())
                .Aggregate((x, y) => x.Concat(y).ToArray())
                .GetAwaiter();

            _images = _enable
                .Where(enable => enable)
                .Select(_ => Observable
                    .Using(
                        () => int.TryParse(Source, out int number) ? new Capture(number, true) : new Capture(Source, true),
                        capture => Observable
                            .FromEventPattern(h => capture.ImageGrabbed += h, h => capture.ImageGrabbed -= h)
                            .Sample(TimeSpan.FromMilliseconds(_frameInterval))
                            .Select(x => GrabImage(capture)))
                        .TakeUntil(_enable.Where(enable => !enable))
                        .Catch(Observable.Empty<Image<Bgr, byte>>()))
                .Switch()
                .Publish();

            var imageSize = _images
                .Select(i => i.Copy())
                .Select(i => i.Size)
                .Take(1)
                .GetAwaiter();

            _patterns
                .Scan(new Pattern[0], (patterns, pattern) => patterns.Concat(new[] { pattern }).Take(_numberOfPatterns).ToArray())
                .Where(patterns => patterns.Length == _numberOfPatterns)
                .Subscribe(patterns => PatternsSet = patterns);                
            
            _images
                .Merge(_patterns.Select(p => p.Image))
                .Select(i => i.Copy().Resize(600, 400, Inter.Linear, true))
                .Subscribe(i => Application.Current?.Dispatcher.Invoke(() => Step1 = i.ToImageSource()));

            _patterns
                .Subscribe(p => PatternQuality = 100 * (int)(p.Corners.Size / (float)(_nCornersHorizontal * _nCornersVertical)));

            _calibrations
                .Subscribe(c => Calibration = c);

            _saveTriggers
                .Subscribe(file => SaveCalibration(Calibration, file));

            _calibrations
                .WithLatestFrom(_images, (c, i) => new { Image = i.Copy(), Calibration = c })
                .Select(d =>
                {
                    var output = d.Image.Clone();
                    CvInvoke.Undistort(d.Image, output, d.Calibration.CameraMatrix, d.Calibration.DistortionCoefficients);
                    return output;
                })
                .Select(i => i.Copy().Resize(600, 400, Inter.Linear, true))
                .Subscribe(i => Application.Current?.Dispatcher.Invoke(() => Step2 = i.ToImageSource()));

            _images.Connect();
        }

        public void SaveCalibration(string file)
        {
            _saveTriggers.OnNext(file);
        }

        public void Start()
        {
            _enable.OnNext(true);
        }

        public void Stop()
        {
            _enable.OnNext(false);
        }

        public void FindPatterns()
        {
            PatternsSet = null;

            Observable
                .Interval(TimeSpan.FromMilliseconds(1000))
                .WithLatestFrom(_images, (_, image) => image)
                .Select(FindPattern)
                .Where(p => p.Found)
                .Take(_numberOfPatterns)
                .Subscribe(_patterns);
        }

        public void OpenCalibration(string file)
        {
            try
            {
                var fs = new FileStorage(file, FileStorage.Mode.Read);
                Calibration = new Calibration();
                fs.GetNode("cameraMatrix").ReadMat(Calibration.CameraMatrix);
                fs.GetNode("distCoeffs").ReadMat(Calibration.DistortionCoefficients);
                fs.GetNode("translationVector").ReadMat(Calibration.TranslationVectors);
                fs.GetNode("rotationVector").ReadMat(Calibration.RotationVectors);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private void SaveCalibration(Calibration c, string file)
        {
            var fs = new FileStorage(file, FileStorage.Mode.Write);
            fs.Write(c.CameraMatrix, "cameraMatrix");
            fs.Write(c.DistortionCoefficients, "distCoeffs");
            
            fs.Write(c.RotationVectors, $"rotationVector");
            fs.Write(c.TranslationVectors, $"translationVector");
        }

        private double Angle(PointF point, Calibration calibration)
        {
            var undistorted = new VectorOfPointF();
            var homogeneous = new VectorOfPoint3D32F();

            // Undistort and make homogeneous
            CvInvoke.UndistortPoints(new VectorOfPointF(new[] { point }), undistorted, calibration.CameraMatrix, calibration.DistortionCoefficients);
            CvInvoke.ConvertPointsToHomogeneous(undistorted, homogeneous);

            // Calculate angle
            var sign = homogeneous[0].X / Math.Abs(homogeneous[0].X);
            return sign * Math.Acos(1 / homogeneous[0].Norm) * 180 / Math.PI;
        }

        public void CalibrateIntrinsic()
        {
            var imageSize = PatternsSet.First().Image.Size;

            _calibrations.OnNext(PatternsSet
                .Select(p => p.Corners)
                .Where(p => p.Size == _nCornersHorizontal * _nCornersVertical)
                .Select(v => v.ToArray())
                .ToObservable()
                .Scan(new PointF[0][], (set, p) => set
                    .Concat(new[] { p })
                    .ToArray())
                .Select(s => CalibrateIntrinsic(s, _realCorners.Repeat(s.Length).ToArray().Wait(), imageSize))
                .Wait());
        }

        public void CalibrateExtrinsic()
        {
            var p = _realLocation;

            var pattern = PatternsSet.First();

            var realCorners = Observable
                .Range(0, _nCornersVertical)
                .SelectMany(z => Observable
                    .Range(0, _nCornersHorizontal)
                    .Select(x => new MCvPoint3D32f(p.X + x * _squareSize, p.Y, p.Z + z * _squareSize))
                    .ToArray())
                .Aggregate((x, y) => x.Concat(y).ToArray())
                .Wait();

            Calibration = CalibrateExtrinsic(Calibration, pattern.Corners.ToArray(), realCorners, pattern.Image.Size);
        }

        private Calibration CalibrateIntrinsic(PointF[][] patterns, MCvPoint3D32f[][] objectPoints, ImageSize size)
        {
            var c = new Calibration();
            c.Error = CvInvoke.CalibrateCamera(objectPoints, patterns, size, c.CameraMatrix, c.DistortionCoefficients, CalibType.RationalModel, new MCvTermCriteria(30, 0.1), out Mat[] rvecs, out Mat[] tvecs);
            return c;
        }

        private Calibration CalibrateExtrinsic(Calibration calibration, PointF[] patterns, MCvPoint3D32f[] objectPoints, ImageSize size)
        {
            var c = new Calibration
            {
                CameraMatrix = calibration.CameraMatrix,
                DistortionCoefficients = calibration.DistortionCoefficients
            };

            if (CvInvoke.SolvePnP(objectPoints, patterns, c.CameraMatrix, c.DistortionCoefficients, c.RotationVectors, c.TranslationVectors))
                return c;
            else
                return null;
        }

        private Image<Bgr, byte> GrabImage(VideoCapture capture)
        {
            try
            {
                var temp = new Image<Bgr, byte>(capture.Width, capture.Height);
                Image<Bgr, byte> image = null;
                capture.Retrieve(temp);

                image = temp.Copy();

                temp.Dispose();
                return image;
            }
            catch (CvException)
            {

            }

            return null;
        }

        private Pattern FindPattern(Image<Bgr, byte> image)
        {
            var watch = Stopwatch.StartNew();
            
            var corners = new VectorOfPointF();
            var grayImage = image.Convert<Gray, byte>();
            var found = CvInvoke.FindChessboardCorners(grayImage, new ImageSize(_nCornersHorizontal, _nCornersVertical), corners, CalibCbType.AdaptiveThresh | CalibCbType.FastCheck | CalibCbType.NormalizeImage);
            if (found)
                CvInvoke.CornerSubPix(grayImage, corners, new ImageSize(5, 5), new ImageSize(-1, -1), new MCvTermCriteria(30, 0.1));
            else
                corners = new VectorOfPointF();

            CvInvoke.DrawChessboardCorners(image, new ImageSize(_nCornersHorizontal, _nCornersVertical), corners, found);

            watch.Stop();

            if (found)
                FaceAngle = watch.ElapsedMilliseconds;

            return new Pattern
            {
                Corners = corners,
                Image = image,
                Found = found
            };
        }
    }
}
