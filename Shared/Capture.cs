using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public class Capture : VideoCapture
    {
        public Capture(string source, bool start) : base(source)
        {
            if (start)
                Start();
        }

        public Capture(int source, bool start) : base(source)
        {
            if (start)
                Start();
        }

        protected override void DisposeObject()
        {
            Stop();

            Task.Delay(1000).Wait();

            base.DisposeObject();
        }
    }
}
