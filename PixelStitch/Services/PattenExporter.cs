using MigraDocCore.DocumentObjectModel.Tables;
using PixelStitch.Models;
using System;
using MdColor = MigraDocCore.DocumentObjectModel.Color;
using MdColors = MigraDocCore.DocumentObjectModel.Colors;
// ---- MigraDocCore (PDF) with aliases to avoid clashes ----
using MDO = MigraDocCore.DocumentObjectModel;
using MDR = MigraDocCore.Rendering;
// ---- WPF color (your canvas) ----
using WColor = System.Windows.Media.Color;


namespace PixelStitch.Services
{
    public enum PatternViewType
    {
        Colour,
        Symbols,
        StitchTypes
    }

    public class PatternExportOptions
    {
        public bool IncludeColourGrid { get; set; }
        public bool IncludeSymbolGrid { get; set; }
        public bool IncludeStitchTypeGrid { get; set; }
        public string OutputPath { get; set; }
    }

    public static class PatternExporter
    {
        public static void ToPdf(Pattern pattern, PatternExportOptions opts)
        {

            if (pattern == null || pattern.Stitches.Count == 0)
                throw new InvalidOperationException("Pattern is empty");

            var doc = new MDO.Document();
            // After creating 'doc'
            doc.Styles["Normal"].Font.Name = "Segoe UI";  // or "Arial"

            doc.Info.Title = "PixelStitch Pattern";
            doc.Info.Subject = "Generated from PixelStitch";
            doc.DefaultPageSetup.Orientation = MDO.Orientation.Portrait;

            if (opts.IncludeColourGrid)
                AddGridPage(doc, pattern, PatternViewType.Colour);

            if (opts.IncludeSymbolGrid)
                AddGridPage(doc, pattern, PatternViewType.Symbols);

            if (opts.IncludeStitchTypeGrid)
                AddGridPage(doc, pattern, PatternViewType.StitchTypes);

            AddLegendPage(doc, pattern);

            var renderer = new MDR.PdfDocumentRenderer(true);
            renderer.Document = doc;
            renderer.RenderDocument();
            renderer.PdfDocument.Save(opts.OutputPath);
        }

        private static void AddGridPage(MDO.Document doc, Pattern pattern, PatternViewType view)
        {
            var section = doc.AddSection();
            section.PageSetup.TopMargin = "1cm";
            section.PageSetup.LeftMargin = "1cm";
            section.PageSetup.RightMargin = "1cm";
            var cellSize = MDO.Unit.FromMillimeter(2.5);
            section.AddParagraph($"Pattern Grid – {view}", "Heading1").Format.SpaceAfter = "0.5cm";

            // Build a grid table
            var table = section.AddTable();
            table.Borders.Width = 0.25;

            for (int x = 0; x < pattern.Width; x++)
                table.AddColumn(cellSize);  // 3mm per cell approx.

            var grid = new Stitch[pattern.Width, pattern.Height];
            foreach (var s in pattern.Stitches)
                if (s.X < pattern.Width && s.Y < pattern.Height)
                    grid[s.X, s.Y] = s;

            for (int y = 0; y < pattern.Height; y++)
            {
                var row = table.AddRow();
                // thicker horizontal line every 10 rows
                if (y % 10 == 0)
                    row.Borders.Top.Width = 0.75;
                table.Rows.Height = cellSize;
                table.Rows.HeightRule = MigraDocCore.DocumentObjectModel.Tables.RowHeightRule.Exactly;
                for (int x = 0; x < pattern.Width; x++)
                {
                    var cell = row.Cells[x];
                    if (x % 10 == 0)
                        cell.Borders.Left.Width = 0.75;
                    cell.Format.Alignment = MigraDocCore.DocumentObjectModel.ParagraphAlignment.Center;
                    cell.VerticalAlignment = MigraDocCore.DocumentObjectModel.Tables.VerticalAlignment.Center;
                    cell.Format.SpaceBefore = 0;
                    cell.Format.SpaceAfter = 0;
                    cell.Format.LeftIndent = 0;
                    cell.Format.RightIndent = 0;
                    cell.Format.LineSpacing = 1; // keeps symbols tight

                    var st = grid[x, y];
                    if (st == null) continue;

                    switch (view)
                    {
                        case PatternViewType.Colour:
                            cell.Shading.Color = ToMigraColor(st.Colour);
                            break;

                        case PatternViewType.Symbols:
                            if (pattern.SymbolMap.TryGetValue(st.DmcCode, out char sym))
                            {
                                var p = cell.AddParagraph(sym.ToString());
                                p.Format.Font.Size = 7; // adjust to taste
                            }

                            break;

                        case PatternViewType.StitchTypes:
                            cell.AddParagraph(st.Type.ToString().Substring(0, 1)); // placeholder
                            break;
                    }
                }
            }
        }

        private static void AddLegendPage(MDO.Document doc, Pattern pattern)
        {
            var sec = doc.AddSection();
            sec.AddParagraph("Colour Legend", "Heading1").Format.SpaceAfter = "0.5cm";

            var table = sec.AddTable();
            table.Borders.Width = 0.25;
            table.AddColumn(MDO.Unit.FromMillimeter(15)); // Symbol
            table.AddColumn(MDO.Unit.FromMillimeter(20)); // Code
            table.AddColumn(MDO.Unit.FromMillimeter(40)); // Name
            table.AddColumn(MDO.Unit.FromMillimeter(15)); // Colour swatch

            var header = table.AddRow();
            header.Shading.Color = MDO.Colors.LightGray;
            header.Cells[0].AddParagraph("Symbol");
            header.Cells[1].AddParagraph("DMC");
            header.Cells[2].AddParagraph("Name");
            header.Cells[3].AddParagraph("Colour");

            // Build a quick lookup from DMC code to palette entry
            var byCode = new Dictionary<string, DmcColor>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var d in pattern.Palette)
                byCode[d.Code] = d;

            // Rows: only include colours actually present in the symbol map (export set)
            foreach (var kvp in pattern.SymbolMap)
            {
                var dmcCode = kvp.Key;
                var sym = kvp.Value;

                if (!byCode.TryGetValue(dmcCode, out var entry))
                    continue; // skip if not found (shouldn't happen if SymbolMap came from Active)

                var row = table.AddRow();
                // Symbol
                var p = row.Cells[0].AddParagraph(sym.ToString());
                p.Format.Font.Size = 9;
                p.Format.Alignment = MigraDocCore.DocumentObjectModel.ParagraphAlignment.Center;
                row.Cells[0].VerticalAlignment = MigraDocCore.DocumentObjectModel.Tables.VerticalAlignment.Center;

                // DMC code
                row.Cells[1].AddParagraph(dmcCode);

                // Name
                row.Cells[2].AddParagraph(entry.Name ?? string.Empty);

                // Colour swatch
                row.Cells[3].Shading.Color = ToMigraColor(entry.Color);
            }

        }

        private static MigraDocCore.DocumentObjectModel.Color ToMigraColor(System.Windows.Media.Color c)
        {
            // Construct a MigraDoc colour manually.
            var mdColor = new MigraDocCore.DocumentObjectModel.Color();
            mdColor.Argb = (uint)((c.A << 24) | (c.R << 16) | (c.G << 8) | c.B);
            return mdColor;
        }



    }
}
