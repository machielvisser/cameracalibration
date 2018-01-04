using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageSize = System.Drawing.Size;

namespace CameraCalibrationTool
{
    /**
     * ToDo:
     * * Get calibration error to zero
     * * Load calibration and multiply normalized point with inverse of camera matrix
     **/
    public class CalibrationToolViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        
        private const int _nCornersHorizontal = 9;
        private const int _nCornersVertical = 6;
        private const float _squareSize = 0.5F;
        
        private const int _sampleRate = 2;

        private string _calibrationFile;

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

        private int _calibrationError;
        public int CalibrationError
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

        private Capture _capture;
        private ISubject<Image<Bgr, byte>> _images = new Subject<Image<Bgr, byte>>();
        private ISubject<int> _saveTriggers = new Subject<int>();

        public CalibrationToolViewModel()
        {
            _calibrationFile = ConfigurationManager.AppSettings["CalibrationFile"];

            var initialCalibration = File.Exists(_calibrationFile) ? ReadCalibration(_calibrationFile) : Enumerable.Empty<Calibration>();

            var realCorners = Observable
                .Range(0, _nCornersVertical)
                .SelectMany(y => Observable
                    .Range(0, _nCornersHorizontal)
                    .Select(x => new MCvPoint3D32f(x * _squareSize, y * _squareSize, 0.0F))
                    .ToArray())
                .Aggregate((x, y) => x.Concat(y).ToArray())
                .GetAwaiter();

            _capture = new Capture(0);
            _capture.ImageGrabbed += ImageGrabbed;
            _capture.Start();

            var imageSize = _images
                .Select(i => i.Copy())
                .Select(i => i.Size)
                .Take(1)
                .GetAwaiter();

            //_images
            //    .Select(i => i.Copy())
            //    .Subscribe(i => Application.Current.Dispatcher.Invoke(() => Step1 = ImageToImageSource(i)));

            var patterns = _images
                .Select(i => i.Copy())
                .Select(FindPattern)
                .Publish();
            
            patterns
                .Subscribe(i => Application.Current.Dispatcher.Invoke(() => Step1 = ImageToImageSource(i.Image)));

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
                //.Do(c => Debug.WriteLine(c.Error))
                .StartWith(initialCalibration)
                .Publish();

            calibrations
                .Subscribe(c => CalibrationError = (int)c.Error);

            _saveTriggers
                .WithLatestFrom(calibrations, (t, c) => c)
                .Subscribe(c => SaveCalibration(c.CameraMatrix, c.DistortionCoefficients));

            _images
                .Select(i => i.Copy())
                .WithLatestFrom(calibrations, (i, c) => new { Image = i, Calibration = c })
                .Select(d =>
                {
                    var output = d.Image.Clone();
                    CvInvoke.Undistort(d.Image, output, d.Calibration.CameraMatrix, d.Calibration.DistortionCoefficients);
                    return output;
                })
                .Subscribe(i => Application.Current.Dispatcher.Invoke(() => Step2 = ImageToImageSource(i)));

            calibrations
                .Select(c => Angle(new PointF(640, 240), c))
                .Subscribe(a => Debug.WriteLine(a));
            
            patterns.Connect();
            calibrations.Connect();
        }

        ~CalibrationToolViewModel()
        {
            _capture.Stop();
        }

        public void SaveCalibration()
        {
            _saveTriggers.OnNext(0);
        }

        private void SaveCalibration(Mat cameraMatrix, Mat distCoeffs)
        {
            var fs = new FileStorage(_calibrationFile, FileStorage.Mode.Write);
            fs.Write(cameraMatrix, "cameraMatrix");
            fs.Write(distCoeffs, "distCoeffs");
        }

        private IEnumerable<Calibration> ReadCalibration(string file)
        {
            var fs = new FileStorage(file, FileStorage.Mode.Read);
            var calibration = new Calibration();
            fs.GetNode("cameraMatrix").ReadMat(calibration.CameraMatrix);
            fs.GetNode("distCoeffs").ReadMat(calibration.DistortionCoefficients);

            yield return calibration;
        }

        private double Angle(PointF point, Calibration calibration)
        {
            var undistorted = new VectorOfPointF();
            var homogeneous = new VectorOfPoint3D32F();

            // Undistort and make homogeneous
            CvInvoke.UndistortPoints(new VectorOfPointF(new[] { point }), undistorted, calibration.CameraMatrix, calibration.DistortionCoefficients);
            CvInvoke.ConvertPointsToHomogeneous(undistorted, homogeneous);

            // Calculate angle
            var angle = Math.Acos(1 / homogeneous[0].Norm) * 180 / Math.PI;
            return angle;

            // Calculate area in camera coordinates

            // Calculate area in world coordinates

            // Look for possible matches in tracking
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
            IOutputArray image = new Image<Bgr, byte>(_capture.Width, _capture.Height);
            _capture.Retrieve(image);
            _images.OnNext((Image<Bgr, byte>)image);
            ((Image<Bgr, byte>)image).Dispose();
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

        private BitmapImage ImageToImageSource(Image<Bgr, byte> image)
        {
            var bitmap = image.Bitmap;

            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }
    }

    class Pattern
    {
        public VectorOfPointF Corners;
        public Image<Bgr, byte> Image;
    }

    class Calibration
    {
        public Mat CameraMatrix;
        public Mat DistortionCoefficients;

        private Mat _inverseCameraMatrix;
        public Mat InverseCameraMatrix
        {
            get
            {
                if (_inverseCameraMatrix == null)
                {
                    _inverseCameraMatrix = new Mat(3, 3, DepthType.Cv64F, 1);
                    CvInvoke.Invert(CameraMatrix, _inverseCameraMatrix, DecompMethod.LU);
                }
                return _inverseCameraMatrix;
            }
        }
        public double Error;
        
        public Calibration()
        {
            CameraMatrix = new Mat(3, 3, DepthType.Cv64F, 1);
            DistortionCoefficients = new Mat(8, 1, DepthType.Cv64F, 1);
        }
    }
}
