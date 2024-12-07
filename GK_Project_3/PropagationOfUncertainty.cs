using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static GK_Project_3.PropagationOfUncertainty;

namespace GK_Project_3
{
    public static class BitmapSourceHelper
    {
        public static void CopyPixels(this BitmapSource source, PixelColor[,] pixels, int stride, int offset, bool dummy)
        {
            var height = source.PixelHeight;
            var width = source.PixelWidth;
            var pixelBytes = new byte[height * width * 4];
            source.CopyPixels(pixelBytes, stride, 0);
            int y0 = offset / width;
            int x0 = offset - width * y0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    pixels[x + x0, y + y0] = new PixelColor
                    {
                        Blue = pixelBytes[(y * width + x) * 4 + 0],
                        Green = pixelBytes[(y * width + x) * 4 + 1],
                        Red = pixelBytes[(y * width + x) * 4 + 2],
                        Alpha = pixelBytes[(y * width + x) * 4 + 3],
                    };
            }
        }
    }
    public class PropagationOfUncertainty
    {
        private const double _minimumColorDifference = 50.0;
        private const bool _reduceColorsForErrorPropagation = false;

        public static Dictionary<(int x, int y), double> FloydSteinbergMatrix = new Dictionary<(int x, int y), double>()
        {
            {(1,0), 7d/16 },
            {(-1, -1), 3d/16},
            {(0, -1), 5d/16 },
            {(1, -1), 1d/16},
        };

        public static Dictionary<(int x, int y), double> BurkesMatrix = new Dictionary<(int x, int y), double>()
        {
            {(1,0), 8d/32 },
            {(2, 0), 4d/32},
            {(-2, -1), 2d/32},
            {(-1, -1), 4d/32},
            {(0, -1), 8d/32},
            {(1, -1), 4d/32},
            {(2, -1), 2d/ 32},
        };

        public static Dictionary<(int x, int y), double> StuckyMatrix = new Dictionary<(int x, int y), double>()
        {
            {(1,0), 8d/42},
            {(2, 0), 4d/42},
            {(-2, -1), 2d/42},
            {(-1, -1), 4d/42},
            {(0, -1), 8d/42},
            {(1, -1), 4d/42},
            {(2, -1), 2d/ 42},
            {(-2, -2), 1d/42},
            {(-1, -2), 2d/42},
            {(0, -2), 4d/42},
            {(1, -2), 2d/42},
            {(2, -2), 1d/42},
        };

        public static Dictionary<(int x, int y), double> GetFilterMatrix(MainWindow.POUMatrix matrix)
        {
            switch (matrix)
            {
                case MainWindow.POUMatrix.FLOYD_STEINBERG:
                    return FloydSteinbergMatrix;
                case MainWindow.POUMatrix.BURKE:
                    return BurkesMatrix;
                case MainWindow.POUMatrix.STUCKY:
                    return StuckyMatrix;
                default:
                    throw new ArgumentException("Argument is of the POUMatrix enum class with an unknown value");
            }
        }

        
        public static PixelColor[] CreatePalette(int numberOfColors)
        {

            if(_reduceColorsForErrorPropagation){
                int i = 0;
                for (; i * i * i < numberOfColors; i++)
                {
                    ;
                }
                if (i * i * i != numberOfColors)
                {
                    MessageBox.Show("Could not find equal k values for the desired number of colors. Chosen k: " + i.ToString() + ". Resulting pallete size: " + (i * i * i).ToString());
                }
                numberOfColors = i;
            }

            int step = 255 / (numberOfColors-1);

            ConcurrentBag<PixelColor> colors = [];

            Parallel.For(0, numberOfColors, r =>
            {
                for (int g = 0; g < numberOfColors; g++)
                {
                    for (int b = 0; b < numberOfColors; b++)
                    {
                        byte redValue = (byte)(r * step);
                        byte greenValue = (byte)(g * step);
                        byte blueValue = (byte)(b * step);
                        colors.Add(new PixelColor() { Alpha=255, Red = redValue, Green=greenValue, Blue=blueValue});
                    }
                }
            }
            );

            return [.. colors];

        }

        public static PixelColor[] CreatePalettePopularity(PixelColor[,] pixelArray, int numberOfColors)
        {
            ConcurrentDictionary<PixelColor, int> popularityDict = new ConcurrentDictionary<PixelColor, int>();
            int width = pixelArray.GetLength(0);
            int height = pixelArray.GetLength(1);

            Parallel.For(0, width, x =>
            {
                for(int y = 0; y < height; y++)
                {
                    PixelColor color = pixelArray[x, y];
                    popularityDict.AddOrUpdate(color, 1, (k, v) => v+1);
                }
            });

            List<PixelColor> colors = new List<PixelColor>();
            var sortedDict = popularityDict.OrderBy(p => p.Value).ToList();
            for(int i=0; i<sortedDict.Count && i<numberOfColors; i++)
            {
                colors.Add(sortedDict[i].Key);
            }

            return colors.ToArray();
        }

        public static byte ModByte(byte a, double b)
        {
            return (byte)Math.Max(0, Math.Min(255, a + b));
        }

        public static void Propagate(int x, int y, ref PixelColor[,] image, PixelColor newColor, MainWindow.POUMatrix filterMatrix)
        {
            Dictionary<(int x, int y), double> filter = GetFilterMatrix(filterMatrix);
            double redError = newColor.Red - image[x, y].Red;
            double greenError = newColor.Green - image[x, y].Green;
            double blueError = newColor.Blue - image[x, y].Blue;
            foreach (var item in filter)
            {
                int xOffset = item.Key.x;
                int yOffset = item.Key.y;
                double weight = item.Value;

                if (x + xOffset < 0 || y + yOffset < 0 || x + xOffset >= image.GetLength(0) || y + yOffset >= image.GetLength(1))
                {
                    continue;
                }
                else
                {
                    image[x + xOffset, y + yOffset].Red = ModByte(image[x+xOffset, y+yOffset].Red, redError * weight);
                    image[x + xOffset, y + yOffset].Green = ModByte(image[x + xOffset, y + yOffset].Green, greenError * weight);
                    image[x + xOffset, y + yOffset].Blue = ModByte(image[x + xOffset, y + yOffset].Blue, blueError * weight);
                }
            }
        }

        public static int DistanceSqFromColor(PixelColor source, PixelColor dest)
        {
            int redDistance = source.Red - dest.Red;
            int blueDistance = source.Blue - dest.Blue;
            int greenDistance = source.Green - dest.Green;
            return redDistance*redDistance + greenDistance*greenDistance + blueDistance*blueDistance;
        }

        public static PixelColor GetColorFromPalette(PixelColor[] palette, PixelColor source)
        {
            int minDistance = int.MaxValue;
            int idx = -1;
            int currDistance;
            for(int i = 0; i<palette.Length; i++)
            {
                currDistance = DistanceSqFromColor(source, palette[i]);
                if (currDistance < minDistance)
                {
                    minDistance = currDistance;
                    idx = i;
                }
            }
            if(-1 == idx)
            {
                throw new Exception("Couldn't find any color in palette");
            }
            return palette[idx];
        }

        public static int GetClosestValue(int oldValue, int numberOfColors)
        {
            return (int)Math.Round((double)(oldValue * (numberOfColors - 1))) / (numberOfColors - 1);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PixelColor
        {
            public byte Blue;
            public byte Green;
            public byte Red;
            public byte Alpha;
        }

        public static PixelColor[,] GetPixels(BitmapSource source)
        {
            if (source.Format != PixelFormats.Bgra32)
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            PixelColor[,] result = new PixelColor[width, height];

            int bitsPerPixel = source.Format.BitsPerPixel;
            int bytesPerPixel = (bitsPerPixel + 7) / 8;
            int stride = 4 * ((width * bytesPerPixel + 3) / 4);

            source.CopyPixels(result, width*4, 0, false);
            return result;
        }

        
        public static void PutPixels(WriteableBitmap bitmap, PixelColor[,] pixels, int x, int y)
        {
            int width = pixels.GetLength(0);
            int height = pixels.GetLength(1);

            int index = (y * width + x) * 4;

            bitmap.Lock();
            IntPtr backBuffer = bitmap.BackBuffer;

            Marshal.Copy(new byte[] { pixels[x, y].Blue, pixels[x, y].Green, pixels[x, y].Red, pixels[x, y].Alpha }, 0, IntPtr.Add(backBuffer, index), 4);
            bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
            bitmap.Unlock();
        }

        public static WriteableBitmap Dithering(Uri uri, int numberOfColors, MainWindow.POUMatrix filterMatrix)
        {
            BitmapImage bmi = new();
            bmi.BeginInit();
            bmi.UriSource = uri;
            bmi.EndInit();
            bmi.Freeze();

            WriteableBitmap wbm = new WriteableBitmap(bmi);
            int width = wbm.PixelWidth;
            int height = wbm.PixelHeight;
            PixelColor[] palette = CreatePalette(numberOfColors);
            PixelColor[,] pixelArray = GetPixels(bmi);

            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    PixelColor newColor = GetColorFromPalette(palette, pixelArray[x, y]);
                    Propagate(x, y, ref pixelArray, newColor, filterMatrix);
                    pixelArray[x, y] = newColor;
                    PutPixels(wbm, pixelArray, x, y); 
                }
            }

            return wbm;
        }

        public static PixelColor[] KMeansIteration(PixelColor[,] pixelArray, PixelColor[] palette) 
        {
            return GetCentroids(GenerateClusters(pixelArray, palette));
        }

        public static Dictionary<PixelColor, List<PixelColor>> GenerateClusters(PixelColor[,] pixelArray, PixelColor[] palette) 
        {
            Dictionary<PixelColor, List <PixelColor>> clusters = [];
            foreach (PixelColor color in palette)
            {
                clusters[color] = [];
            }

            for (int x = 0; x < pixelArray.GetLength(0); x++)
            {
                for(int y = 0; y < pixelArray.GetLength(1); y++)
                {
                    PixelColor index = GetColorFromPalette(palette, pixelArray[x, y]);
                    clusters[index].Add(pixelArray[x, y]);
                }
            }
            return clusters;
        }

        public static PixelColor[] GetCentroids(Dictionary<PixelColor, List<PixelColor>> clusters)
        {
            PixelColor[] centroids = new PixelColor[clusters.Count];
            int idx = 0;
            foreach(var list in clusters.Values)
            {
                var reds = from item in list select (double)item.Red;
                var greens = from item in list select (double)item.Green;
                var blues = from item in list select (double)item.Blue;

                byte averageRed = (byte)reds.Average();
                byte averageGreen = (byte)greens.Average();
                byte averageBlue = (byte)blues.Average();

                centroids[idx++] = new PixelColor() { Alpha = 255, Red = averageRed, Green = averageGreen, Blue = averageBlue };
            }
            return centroids;
        }

        public static PixelColor[] CreatePaletteKMeans(PixelColor[,] pixelArray, int numberOfColors, int epsilon)
        {
            Random random = new Random();
            int width = pixelArray.GetLength(0);
            int height = pixelArray.GetLength(1);

            List<PixelColor> initialPalette = [];
            for (int i = 0; i < numberOfColors; i++)
            {
                int x, y;
                do
                {
                    x = random.Next(width);
                    y = random.Next(height);
                }
                while (initialPalette.Any((pC) => DistanceSqFromColor(pC, pixelArray[x, y]) <= _minimumColorDifference));
                initialPalette.Add(pixelArray[x, y]);
            }
            PixelColor[] palette = [.. initialPalette];
            for(int i = 0; i<epsilon; i++)
            {
                palette = KMeansIteration(pixelArray, palette);
            }
            return palette;
        }

        public static WriteableBitmap Dithering(Uri uri, int numberOfColors)
        {
            BitmapImage bmi = new();
            bmi.BeginInit();
            bmi.UriSource = uri;
            bmi.EndInit();
            bmi.Freeze();

            WriteableBitmap wbm = new WriteableBitmap(bmi);
            int width = wbm.PixelWidth;
            int height = wbm.PixelHeight;
            
            PixelColor[,] pixelArray = GetPixels(bmi);
            PixelColor[] palette = CreatePalettePopularity(pixelArray, numberOfColors);

            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    PixelColor newColor = GetColorFromPalette(palette, pixelArray[x, y]);
                    pixelArray[x, y] = newColor;
                    PutPixels(wbm, pixelArray, x, y);
                }
            }

            return wbm;
        }

        public static WriteableBitmap Dithering(Uri uri, int numberOfColors, int epsilon)
        {
            BitmapImage bmi = new();
            bmi.BeginInit();
            bmi.UriSource = uri;
            bmi.EndInit();
            bmi.Freeze();

            WriteableBitmap wbm = new WriteableBitmap(bmi);
            int width = wbm.PixelWidth;
            int height = wbm.PixelHeight;

            PixelColor[,] pixelArray = GetPixels(bmi);
            PixelColor[] palette = CreatePaletteKMeans(pixelArray, numberOfColors, epsilon);

            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    PixelColor newColor = GetColorFromPalette(palette, pixelArray[x, y]);
                    pixelArray[x, y] = newColor;
                    PutPixels(wbm, pixelArray, x, y);
                }
            }

            return wbm;
        }

    }
}
