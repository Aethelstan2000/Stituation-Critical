using System.Windows.Media;

namespace StituationCritical.Models
{
    public enum StitchType
    {
        None = 0,
        Full,
        Half,
        ThreeQuarter,
        Quarter,
        Backstitch,
        FrenchKnot
    }

    public class Stitch
    {
        public int X { get; set; }
        public int Y { get; set; }
        public StitchType Type { get; set; } = StitchType.Full;

        // DMC reference or direct colour
        public string DmcCode { get; set; }
        public Color Colour { get; set; }

        // For backstitch: end coordinates (fractional cell coordinates)
        public double X2 { get; set; }
        public double Y2 { get; set; }

        public override string ToString() =>
            $"{Type} Stitch ({DmcCode}) at ({X},{Y})";
    }
}
