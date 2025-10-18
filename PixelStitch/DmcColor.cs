using System.Globalization;
using System.Windows.Media;

namespace StituationCritical
{
    public class DmcColor
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public Color Color { get; set; }
        public int Count { get; set; } = 0;
        public bool IsLocked { get; set; }   // default false

        public string? Symbol { get; set; } // used only in Active Palette / export

        public DmcColor Clone() => new DmcColor { Code = Code, Name = Name, Color = Color, Symbol = Symbol };

        public static DmcColor? ParseCsv(string line)
        {
            var parts = line.Split(','); if (parts.Length < 5) return null;
            if (!byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)) return null;
            if (!byte.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var g)) return null;
            if (!byte.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b)) return null;
            return new DmcColor { Code = parts[0].Trim(), Name = parts[1].Trim(), Color = Color.FromRgb(r, g, b) };
        }
    }
}
