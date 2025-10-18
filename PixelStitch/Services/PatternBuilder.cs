using System.Collections.Generic;
using System.Windows.Media;
using StituationCritical.Models;

namespace StituationCritical.Services
{
    public static class PatternBuilder
    {
        public static Pattern FromCanvas(PixelCanvas canvas, List<DmcColor> activePalette)
        {
            int w = canvas.WidthPixels;
            int h = canvas.HeightPixels;

            var pattern = new Pattern { Width = w, Height = h };
            pattern.Palette = new List<DmcColor>(activePalette);

            // Build SymbolMap from the active palette (user-chosen symbols)
            pattern.SymbolMap.Clear();
            foreach (var d in activePalette)
            {
                char sym = '?';
                if (!string.IsNullOrEmpty(d.Symbol))
                    sym = d.Symbol[0];

                // ensure uniqueness (simple bump if duplicate)
                while (pattern.SymbolMap.ContainsValue(sym))
                    sym = (char)(sym + 1);

                pattern.SymbolMap[d.Code] = sym;
            }

            // NEW: fast stitch lookup grid for the exporter/tiler
            var grid = new Stitch[w, h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = canvas.GetPixel(x, y);
                    if (c.A == 0) continue;

                    var nearest = MainWindow.NearestInPalette(c, activePalette);
                    if (nearest == null) continue;

                    var st = new Stitch
                    {
                        X = x,
                        Y = y,
                        Type = StitchType.Full,
                        Colour = nearest.Color,
                        DmcCode = nearest.Code
                    };

                    pattern.Stitches.Add(st);
                    grid[x, y] = st; // populate the grid for O(1) access
                }
            }

            pattern.StitchGrid = grid; // expose to exporter/tiler
            return pattern;
        }

    }
}
