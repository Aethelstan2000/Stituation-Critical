using MigraDocCore.DocumentObjectModel.Tables;
using StituationCritical.Models;
using System;
using MdColor = MigraDocCore.DocumentObjectModel.Color;
using MdColors = MigraDocCore.DocumentObjectModel.Colors;
// ---- MigraDocCore (PDF) with aliases to avoid clashes ----
using MDO = MigraDocCore.DocumentObjectModel;
using MDR = MigraDocCore.Rendering;
// ---- WPF color (your canvas) ----
using WColor = System.Windows.Media.Color;


namespace StituationCritical.Services
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
        public double CellSizemm { get; set; } = 2.5;
        public int Dpi { get; set; } = 144;
        public double MarginCm { get; set; } = 1.5;
        public double HeaderCm { get; set; } = 1.2;
        public int OverlapCells { get; set; } = 1;   // 0..2 recommended
        public bool ShowRulers { get; set; } = true;
        public bool FitToSinglePageIfPossible { get; set; } = false;
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
            doc.Styles["Normal"].Font.Name = "Segoe UI Symbol"; // good coverage on Windows

            doc.Info.Title = "StituationCritical Pattern";
            doc.Info.Subject = "Generated from StituationCritical";
            doc.DefaultPageSetup.Orientation = MDO.Orientation.Portrait;

            if (opts.IncludeColourGrid)
                AddTiledGridPages(doc, pattern, PatternViewType.Colour, opts);

            if (opts.IncludeSymbolGrid)
                AddTiledGridPages(doc, pattern, PatternViewType.Symbols, opts);

            if (opts.IncludeStitchTypeGrid)
                AddTiledGridPages(doc, pattern, PatternViewType.StitchTypes, opts);


            AddLegendPage(doc, pattern);

            var renderer = new MDR.PdfDocumentRenderer(true);
            renderer.Document = doc;
            renderer.RenderDocument();
            renderer.PdfDocument.Save(opts.OutputPath);
        }
        private static void AddTiledGridPages(MDO.Document doc, Pattern pattern, PatternViewType view, PatternExportOptions opts)
        {
            // Page geometry (points)
            var setup = doc.DefaultPageSetup;
            double pageW = setup.PageWidth.Point;
            double pageH = setup.PageHeight.Point;

            double marginPt = MDO.Unit.FromCentimeter(opts.MarginCm).Point;
            double headerPt = MDO.Unit.FromCentimeter(opts.HeaderCm).Point;

            double contentW = pageW - 2 * marginPt;
            double contentH = pageH - (2 * marginPt + headerPt);

            // Cell size (points). 1 mm ≈ 2.83465 pt
            double cellPt = Math.Max(2.0, opts.CellSizemm * 2.83465);

            // How many cells fit per page (at this cell size)
            int cellsX = Math.Max(1, (int)Math.Floor(contentW / cellPt));
            int cellsY = Math.Max(1, (int)Math.Floor(contentH / cellPt));

            // Step with overlap
            int overlap = Math.Max(0, opts.OverlapCells);
            int stepX = Math.Max(1, cellsX - overlap);
            int stepY = Math.Max(1, cellsY - overlap);

            for (int y0 = 0; y0 < pattern.Height; y0 += stepY)
            {
                int yCount = Math.Min(cellsY, pattern.Height - y0);
                for (int x0 = 0; x0 < pattern.Width; x0 += stepX)
                {
                    int xCount = Math.Min(cellsX, pattern.Width - x0);
                    RenderTilePage(doc, pattern, view, x0, y0, xCount, yCount, cellPt, marginPt, headerPt);
                }
            }
        }

        private static void RenderTilePage(
            MDO.Document doc,
            Pattern pattern,
            PatternViewType view,
            int x0, int y0, int xCount, int yCount,
            double cellPt, double marginPt, double headerPt)
        {
            var section = doc.AddSection();

            // Margins (we keep defaults from Document; header spacing is handled via paragraphs)
            var title = section.AddParagraph($"Pattern Grid — {view}");
            title.Style = "Heading1";
            title.Format.SpaceAfter = "0.2cm";

            var range = section.AddParagraph($"Columns {x0 + 1}–{x0 + xCount}, Rows {y0 + 1}–{y0 + yCount}");
            range.Format.SpaceAfter = "0.5cm";
            range.Format.Font.Size = 9;

            // Build a square-cell table for this tile
            var table = section.AddTable();
            table.Borders.Width = 0; // base hairlines off (reduces shimmer)
            table.Rows.LeftIndent = 0;
            table.Rows.HeightRule = MDO.Tables.RowHeightRule.Exactly
;

            for (int x = 0; x < xCount; x++)
                table.AddColumn(MDO.Unit.FromPoint(cellPt));

            // Pre-build a quick grid lookup for this tile
            // (optional, you can also index directly from pattern.Stitches)
            // We'll render directly by probing each (x,y).

            double heavy = 1.0; // pt for 10×10 lines
            double thin = 0.25;

            for (int ry = 0; ry < yCount; ry++)
            {
                int gy = y0 + ry; // global Y
                var row = table.AddRow();
                row.Height = MDO.Unit.FromPoint(cellPt);

                // Thick horizontal line every 10 global rows
                if (gy % 10 == 0)
                    row.Borders.Top.Width = heavy;
                else
                    row.Borders.Top.Width = thin;

                for (int rx = 0; rx < xCount; rx++)
                {
                    int gx = x0 + rx; // global X
                    var cell = row.Cells[rx];

                    // Thick vertical line every 10 global cols
                    if (gx % 10 == 0)
                        cell.Borders.Left.Width = heavy;
                    else
                        cell.Borders.Left.Width = thin;

                    // Fetch stitch at (gx, gy)
                    Stitch st = null;
                    // Option A: quick linear search (OK for now)
                    // (Better: prepare a Stitch[,] grid once in PatternBuilder)
                    // We'll do a simple find here:
                    // NOTE: if many stitches, consider a dictionary or 2D array cache.
                    // For now:
                    // st = pattern.Stitches.FirstOrDefault(s => s.X == gx && s.Y == gy);

                    
                    // Efficient version: assume pattern has StitchGrid[,] (add later if you like)
                    if (pattern.StitchGrid != null)
                    {
                        st = pattern.StitchGrid[gx, gy];
                    }
                    else
                    {
                        // Fallback: try to find quickly (still OK for moderate sizes)
                        // You can replace this with a precomputed map if needed.
                        foreach (var s in pattern.Stitches)
                        {
                            if (s.X == gx && s.Y == gy) { st = s; break; }
                        }
                    }
                    
                    if (st == null) continue;

                    switch (view)
                    {
                        case PatternViewType.Colour:
                            cell.Shading.Color = ToMigraColor(st.Colour);
                            break;

                        case PatternViewType.Symbols:
                            if (!string.IsNullOrEmpty(st.DmcCode) &&
                                pattern.SymbolMap.TryGetValue(st.DmcCode, out var sym))
                            {
                                var p = cell.AddParagraph(sym.ToString());
                                // Font size ~80% of cell size, clamped for legibility
                                double fontPt = Math.Clamp(cellPt * 0.8, 5.0, 10.0);
                                p.Format.Font.Size = fontPt;
                                p.Format.Alignment = MDO.ParagraphAlignment.Center;
                                cell.VerticalAlignment = MDO.Tables.VerticalAlignment.Center;
                                cell.Format.SpaceBefore = 0;
                                cell.Format.SpaceAfter = 0;
                                cell.Format.LeftIndent = 0;
                                cell.Format.RightIndent = 0;
                            }
                            break;

                        case PatternViewType.StitchTypes:
                            // Placeholder for now: one-letter mark. Replace when types are implemented.
                            var q = cell.AddParagraph(st.Type.ToString().Substring(0, 1));
                            double f2 = Math.Clamp(cellPt * 0.75, 5.0, 10.0);
                            q.Format.Font.Size = f2;
                            q.Format.Alignment = MDO.ParagraphAlignment.Center;
                            cell.VerticalAlignment = MDO.Tables.VerticalAlignment.Center;
                            cell.Format.SpaceBefore = 0;
                            cell.Format.SpaceAfter = 0;
                            cell.Format.LeftIndent = 0;
                            cell.Format.RightIndent = 0;
                            break;
                    }
                }
            }

            // Draw outer border on the right/bottom edges of the tile
            if (table.Rows.Count > 0)
            {
                var lastRow = table.Rows[table.Rows.Count - 1];
                lastRow.Borders.Bottom.Width = heavy;
                for (int rx = 0; rx < xCount; rx++)
                    table.Rows[0].Cells[rx].Borders.Top.Width = (y0 % 10 == 0) ? heavy : thin;

                for (int ry = 0; ry < yCount; ry++)
                {
                    var c = table.Rows[ry].Cells[xCount - 1];
                    c.Borders.Right.Width = heavy;
                }
            }
        }

        private static void AddLegendPage(MDO.Document doc, Pattern pattern)
        {
            var sec = doc.AddSection();
            var title = sec.AddParagraph("Colour Legend", "Heading1");
            title.Format.SpaceAfter = "0.5cm";

            // Build lookups
            var byCode = new Dictionary<string, DmcColor>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var d in pattern.Palette)
                byCode[d.Code] = d;

            // Count stitches per DMC code
            var counts = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            int total = 0;
            foreach (var s in pattern.Stitches)
            {
                if (string.IsNullOrEmpty(s.DmcCode)) continue;
                // If you only want FULL stitches counted, uncomment:
                // if (s.Type != StitchType.Full) continue;

                total++;
                counts.TryGetValue(s.DmcCode, out int c);
                counts[s.DmcCode] = c + 1;
            }

            // Layout table
            var table = sec.AddTable();
            table.Borders.Width = 0.25;

            table.AddColumn(MDO.Unit.FromMillimeter(12)); // Symbol
            table.AddColumn(MDO.Unit.FromMillimeter(18)); // DMC
            table.AddColumn(MDO.Unit.FromMillimeter(80)); // Name
            table.AddColumn(MDO.Unit.FromMillimeter(18)); // Stitches
            //table.AddColumn(MDO.Unit.FromMillimeter(12)); // %
            table.AddColumn(MDO.Unit.FromMillimeter(15)); // Swatch

            var header = table.AddRow();
            header.Shading.Color = ToMigraColor(System.Windows.Media.Colors.LightGray);
            header.Cells[0].AddParagraph("Symbol");
            header.Cells[1].AddParagraph("DMC");
            header.Cells[2].AddParagraph("Name");
            header.Cells[3].AddParagraph("Stitches");
            //header.Cells[4].AddParagraph("%");
            header.Cells[4].AddParagraph("Colour");

            // Generate rows (sorted by count desc; remove OrderBy if you want palette order)
            foreach (var kv in counts.OrderByDescending(k => k.Value))
            {
                var code = kv.Key;
                var count = kv.Value;
                var pct = (total > 0) ? (100.0 * count / total) : 0.0;

                byCode.TryGetValue(code, out var entry);

                var row = table.AddRow();

                // Symbol (centered)
                char sym = pattern.SymbolMap.TryGetValue(code, out var s) ? s : '?';
                var pSym = row.Cells[0].AddParagraph(sym.ToString());
                pSym.Format.Font.Name = "Segoe UI Symbol";
                pSym.Format.Font.Size = 9;
                pSym.Format.Alignment = MDO.ParagraphAlignment.Center;
                row.Cells[0].VerticalAlignment = MDO.Tables.VerticalAlignment.Center;

                // DMC code
                row.Cells[1].AddParagraph(code ?? "");

                // Name
                row.Cells[2].AddParagraph(entry?.Name ?? "");

                // Stitches (right-aligned)
                var pCnt = row.Cells[3].AddParagraph(count.ToString());
                pCnt.Format.Alignment = MDO.ParagraphAlignment.Right;

                // Percentage (right-aligned, 0.0 format)
                //var pPct = row.Cells[4].AddParagraph(pct.ToString("0.0"));
                //pPct.Format.Alignment = MDO.ParagraphAlignment.Right;

                // Colour swatch
                if (entry != null)
                    row.Cells[4].Shading.Color = ToMigraColor(entry.Color);

                // Tighten cell spacing
                for (int i = 0; i < row.Cells.Count; i++)
                {
                    var c = row.Cells[i];
                    c.Format.SpaceBefore = 0;
                    c.Format.SpaceAfter = 0;
                    c.Format.LeftIndent = 0;
                    c.Format.RightIndent = 0;
                    c.VerticalAlignment = MDO.Tables.VerticalAlignment.Center;
                }
            }

            // Optional summary footer
            var summary = sec.AddParagraph($"\nTotal stitches: {total}");
            summary.Format.Font.Size = 9;
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
