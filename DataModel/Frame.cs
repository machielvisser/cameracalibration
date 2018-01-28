using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataModel
{
    public class Frame<TColor, TDepth> 
        where TColor : struct, IColor
        where TDepth : new()

    {
        public Image<TColor, TDepth> Image { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
