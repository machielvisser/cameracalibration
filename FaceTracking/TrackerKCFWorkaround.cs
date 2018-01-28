using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Tracking;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FaceTracking
{
    public class TrackerKCFWorkaround : TrackerKCF
    {

        // Necessary to work around a bug in DisposeObject of the TrackerKCF class that prevents the release of the unmanaged memory.
        protected override void DisposeObject()
        {
            if (_ptr != IntPtr.Zero)
                cveTrackerKCFRelease(ref _ptr);

            _trackerPtr = IntPtr.Zero;
        }

        [DllImport(CvInvoke.ExternLibrary, CallingConvention = CvInvoke.CvCallingConvention)]
        private static extern void cveTrackerKCFRelease(ref IntPtr tracker);
    }
}