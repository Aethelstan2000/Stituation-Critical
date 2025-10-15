using System.Collections.Generic;

namespace PixelStitch.Models
{
    public class Pattern
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<Stitch> Stitches { get; set; } = new List<Stitch>();
        public List<DmcColor> Palette { get; set; } = new(); // active palette snapshot used to build the pattern
        public Stitch[,] StitchGrid { get; set; }

        // later additions:
        // public List<Backstitch> Backstitches { get; set; }
        // public Dictionary<string, char> Symbols { get; set; }
        public Dictionary<string, char> SymbolMap { get; set; } = new();

        public override string ToString() =>
            $"Pattern: {Width}×{Height}, {Stitches.Count} stitches";
    }
}

