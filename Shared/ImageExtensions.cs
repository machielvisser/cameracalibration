using Emgu.CV;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace Shared
{
    public static class ImageExtensions
    {
        public static BitmapImage ToImageSource<TColor>(this Image<TColor, byte> image) where TColor : struct, IColor
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
}
