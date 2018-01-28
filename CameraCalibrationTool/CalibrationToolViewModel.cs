using DataModel;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Shared;
using System;
using System.ComponentModel;
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
        private const float _squareSize = 0.5F;
        
        private const int _sampleRate = 2;

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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Step1)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Step2)));
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

        private double _calibrationError;
        public double CalibrationError
        {
            get
            {
                return _calibrationError;
            }
            set
            {
                _calibrationError = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CalibrationError)));
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

        private VideoCapture _capture;

        private ISubject<Image<Bgr, byte>> _images = new Subject<Image<Bgr, byte>>();
        private ISubject<string> _saveTriggers = new Subject<string>();
        private ISubject<Calibration> _calibrations = new Subject<Calibration>();

        public CalibrationToolViewModel()
        {
            _capture = new VideoCapture(0);
            _capture.ImageGrabbed += ImageGrabbed;
            _capture.Start();

            var realCorners = Observable
                .Range(0, _nCornersVertical)
                .SelectMany(y => Observable
                    .Range(0, _nCornersHorizontal)
                    .Select(x => new MCvPoint3D32f(x * _squareSize, y * _squareSize, 0.0F))
                    .ToArray())
                .Aggregate((x, y) => x.Concat(y).ToArray())
                .GetAwaiter();

            var imageSize = _images
                .Select(i => i.Copy())
                .Select(i => i.Size)
                .Take(1)
                .GetAwaiter();

            var patterns = _images
                .Select(i => i.Copy())
                .Sample(TimeSpan.FromMilliseconds(250))
                .Select(FindPattern)
                .Publish();
            
            patterns
                .Subscribe(i => Application.Current?.Dispatcher.Invoke(() => Step1 = i.Image.ToImageSource()));

            patterns
                .Subscribe(p => PatternQuality = 100 * (int)(p.Corners.Size / (float)(_nCornersHorizontal * _nCornersVertical)));

            var calibrations = patterns
                .Sample(TimeSpan.FromSeconds(_sampleRate))
                .Select(p => p.Corners)
                .Where(p => p.Size == _nCornersHorizontal * _nCornersVertical)
                .Select(v => v.ToArray())
                .Scan(new PointF[0][], (set, p) => set
                    .Concat(new[] { p })
                    .ToArray())
                .Select(s => Calibrate(s, realCorners, imageSize.Wait()))
                .Merge(_calibrations)
                .Publish();

            calibrations
                .Subscribe(c => CalibrationError = c.Error);

            _saveTriggers
                .WithLatestFrom(calibrations, (f, c) => new { File = f, Calibration = c })
                .Subscribe(t => SaveCalibration(t.Calibration.CameraMatrix, t.Calibration.DistortionCoefficients, t.File));

            _images
                .Select(i => i.Copy())
                .WithLatestFrom(calibrations, (i, c) => new { Image = i, Calibration = c })
                .Select(d =>
                {
                    var output = d.Image.Clone();
                    CvInvoke.Undistort(d.Image, output, d.Calibration.CameraMatrix, d.Calibration.DistortionCoefficients);
                    return output;
                })
                .Subscribe(i => Application.Current?.Dispatcher.Invoke(() => Step2 = i.ToImageSource()));

            patterns.Connect();
            calibrations.Connect();
        }

        ~CalibrationToolViewModel()
        {
            _capture.Stop();
        }

        public void SaveCalibration(string file)
        {
            _saveTriggers.OnNext(file);
        }

        public void OpenCalibration(string file)
        {
            try
            {
                _calibrations.OnNext(ReadCalibration(file));
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private void SaveCalibration(Mat cameraMatrix, Mat distCoeffs, string file)
        {
            var fs = new FileStorage(file, FileStorage.Mode.Write);
            fs.Write(cameraMatrix, "cameraMatrix");
            fs.Write(distCoeffs, "distCoeffs");
        }

        private Calibration ReadCalibration(string file)
        {
            var fs = new FileStorage(file, FileStorage.Mode.Read);
            var calibration = new Calibration();
            fs.GetNode("cameraMatrix").ReadMat(calibration.CameraMatrix);
            fs.GetNode("distCoeffs").ReadMat(calibration.DistortionCoefficients);

            return calibration;
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

        private Calibration Calibrate(PointF[][] patterns, IObservable<MCvPoint3D32f[]> realCorners, ImageSize size)
        {
            var objectPoints = realCorners
                .Repeat(patterns.Length)
                .ToArray()
                .Wait();

            var c = new Calibration();
            Mat[] rvecs, tvecs;
            c.Error = CvInvoke.CalibrateCamera(objectPoints, patterns, size, c.CameraMatrix, c.DistortionCoefficients, CalibType.RationalModel, new MCvTermCriteria(30, 0.1), out rvecs, out tvecs);

            return c;
        }

        private void ImageGrabbed(object sender, EventArgs e)
        {
            var image = new Image<Bgr, byte>(_capture.Width, _capture.Height);
            _capture.Retrieve(image);
            _images.OnNext(image);
            image.Dispose();
        }

        private Pattern FindPattern(Image<Bgr, byte> image)
        {
            var corners = new VectorOfPointF();
            var grayImage = image.Convert<Gray, byte>();
            var found = CvInvoke.FindChessboardCorners(grayImage, new ImageSize(_nCornersHorizontal, _nCornersVertical), corners, CalibCbType.AdaptiveThresh | CalibCbType.FastCheck | CalibCbType.NormalizeImage);
            if (found)
                CvInvoke.CornerSubPix(grayImage, corners, new ImageSize(11, 11), new ImageSize(-1, -1), new MCvTermCriteria(30, 0.1));
            else
                corners = new VectorOfPointF();

            CvInvoke.DrawChessboardCorners(image, new ImageSize(_nCornersHorizontal, _nCornersVertical), corners, found);
            
            return new Pattern
            {
                Corners = corners,
                Image = image
            };
        }
    }
}
