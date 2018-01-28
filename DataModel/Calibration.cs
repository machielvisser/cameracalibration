using Emgu.CV;
using Emgu.CV.CvEnum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataModel
{
    public class Calibration
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
