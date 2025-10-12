using System.Collections.Generic;
using System.Windows.Media;
using PixelStitch.Models;

namespace PixelStitch.Services
{
    public static class PatternBuilder
    {
        public static Pattern FromCanvas(PixelCanvas canvas, List<DmcColor> activePalette)
        {
            int w = canvas.WidthPixels;
            int h = canvas.HeightPixels;

            var pattern = new Pattern { Width = w, Height = h };
            pattern.Palette = new List<DmcColor>(activePalette);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = canvas.GetPixel(x, y);
                    if (c.A == 0) continue;

                    var nearest = MainWindow.NearestInPalette(c, activePalette);
                    if (nearest == null) continue;

                    pattern.Stitches.Add(new Stitch
                    {
                        X = x,
                        Y = y,
                        Type = StitchType.Full,
                        Colour = nearest.Color,
                        DmcCode = nearest.Code
                    });
                }
            }

            return pattern;
        }
    }
}
