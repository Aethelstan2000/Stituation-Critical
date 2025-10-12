using Microsoft.Win32;
using PixelStitch.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PixelStitch
{
    public class PixelStitchProject
    {
        public int Version { get; set; } = 1;
        public int Width { get; set; }
        public int Height { get; set; }
        public double ReferenceOpacity { get; set; }
        public bool ReferenceVisible { get; set; }
        public string PixelLayerPngBase64 { get; set; } = "";
        public string? ReferencePngBase64 { get; set; }
        public List<PaletteEntry>? ActivePalette { get; set; } // Code + Symbol
        public double CanvasOpacity { get; set; } = 1.0;

    }

    public class PaletteEntry
    {
        public string Code { get; set; } = "";
        public string? Symbol { get; set; }
    }

    public partial class MainWindow : Window
    {
        private List<DmcColor> _allDmc = new();
        private List<DmcColor> _active = new();
        private DmcColor? _activeColour;
        // Panning state
        private bool _isPanning = false;
        private Point _panStart;
        private double _panStartH, _panStartV;
        private PixelCanvas.Anchor _resizeAnchor = PixelCanvas.Anchor.Center;

        public List<string> SymbolChoices { get; } = new List<string>
        {
            // Common geometric shapes
            "●","○","■","□","▲","△","▼","▽","◆","◇","★","☆","⬤","⬥","⬧","⬢","⬣",
            "▣","▤","▥","▦","▧","▨","▩","▮","▯","▰","▱",
            "◉","◎","◍","◌","◐","◑","◒","◓","◔","◕","◖","◗",
            "◘","◙","◚","◛","◜","◝","◞","◟",
            "◠","◡","◢","◣","◤","◥","◦","◯","◴","◵","◶","◷",

            // Crosses and stars
            "✚","✖","✛","✜","✢","✣","✤","✥","✦","✧","✩","✪","✫","✬","✭","✮","✯",
            "✰","✱","✲","✳","✴","✵","✶","✷","✸","✹","✺","✻","✼","✽","✾","✿",
            "❀","❁","❂","❃","❄","❅","❆","❇","❈","❉","❊","❋",

            // Squares and blocks
            "▢","▣","▤","▥","▦","▧","▨","▩","▰","▱","▪","▫","◼","◻","◾","◽","◾","◿",

            // Arrows
            "←","↑","→","↓","↔","↕","↖","↗","↘","↙","⇄","⇅","⇆","⇇","⇈","⇉","⇊","⇋","⇌",

            // Triangles and pointers
            "◀","▶","◁","▷","◂","▸","◄","►","▴","▵","▾","▿","◢","◣","◤","◥",

            // Dice-style pips
            "⚀","⚁","⚂","⚃","⚄","⚅",

            // Miscellaneous shapes
            "☐","☑","☒","☓","☩","☮","☯","☸","☹","☺","☻","☼","☽","☾","♠","♣","♥","♦",
            "♤","♧","♡","♢","♩","♪","♫","♬","♭","♮","♯",

            // Greek letters (handy unique glyphs)
            "α","β","γ","δ","ε","ζ","η","θ","ι","κ","λ","μ","ν","ξ","ο","π","ρ","σ","τ","υ","φ","χ","ψ","ω",

            // Uppercase Latin letters
            "A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z",

            // Numbers
            "0","1","2","3","4","5","6","7","8","9"
        };
        private void Anchor_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb && tb.Tag is string tag)
            {
                if (Enum.TryParse<PixelCanvas.Anchor>(tag, out var a))
                    _resizeAnchor = a;
            }
        }
        private void ResizeCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(WidthBox.Text, out int newW) ||
                !int.TryParse(HeightBox.Text, out int newH) ||
                newW < 1 || newH < 1 || newW > 4096 || newH > 4096)
            {
                MessageBox.Show("Enter a valid size (1..4096).");
                return;
            }

            Canvas.ResizeCanvas(newW, newH, _resizeAnchor);
            UpdateActivePaletteCounts();  // keep counts correct
            MarkDirty();                  // flag unsaved changes
        }


        public MainWindow()
        {
            InitializeComponent();
            Canvas.PreviewMouseWheel += Canvas_PreviewMouseWheel;

            // Middle-button panning
            Canvas.MouseDown += Canvas_MouseDown_Pan;
            Canvas.MouseMove += Canvas_MouseMove_Pan;
            Canvas.MouseUp += Canvas_MouseUp_Pan;
            Canvas.MouseLeave += (s, e) => { if (_isPanning) { _isPanning = false; Canvas.ReleaseMouseCapture(); Cursor = Cursors.Arrow; } };

            LoadPalette();
            if (PaletteList.Items.Count > 0) PaletteList.SelectedIndex = 0;
            Canvas.SetZoom((int)ZoomSlider.Value);
            Canvas.StrokeCommitted += (s, e) =>
            {
                UpdateActivePaletteCounts();  // if you added counts
                MarkDirty();                  // set _isDirty = true and add "*" in title
            };


            Canvas.ColorPicked += Canvas_ColorPicked;
            ActiveList.ItemsSource = _active;

            RefOpacitySlider.ValueChanged += (_, __) => MarkDirty();
            CanvasOpacitySlider.ValueChanged += (_, __) => MarkDirty();
            ShowRefCheck.Click += (_, __) => MarkDirty();

        }

        private void MergeSelected_Click(object sender, RoutedEventArgs e)
        {
            // Must pick exactly two colours
            var selected = ActiveList.SelectedItems.Cast<DmcColor>().ToList();
            if (selected.Count != 2)
            {
                MessageBox.Show("Select exactly two colours to merge.\nTip: hold Ctrl to multi-select.");
                return;
            }

            // Convention: first = KEEP, second = DROP
            var keep = selected[0];
            var drop = selected[1];

            if (keep.Code.Equals(drop.Code, StringComparison.InvariantCultureIgnoreCase))
            {
                MessageBox.Show("Those are the same colour.");
                return;
            }

            // Build new pixel buffer by reassigning pixels whose NEAREST ACTIVE is 'drop' to 'keep'
            int w = Canvas.WidthPixels, h = Canvas.HeightPixels;
            var newPixels = new System.Windows.Media.Color[w, h];
            bool anyChange = false;

            // Use the current active palette as the classification set (before removing 'drop')
            // This aligns merge behaviour with UpdateActivePaletteCounts().
            var classifyPalette = _active;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = Canvas.GetPixel(x, y);
                    if (c.A == 0)
                    {
                        newPixels[x, y] = System.Windows.Media.Colors.Transparent;
                        continue;
                    }

                    var nearest = NearestInPalette(c, classifyPalette); // <- your existing helper
                    if (nearest != null && nearest.Code.Equals(drop.Code, StringComparison.InvariantCultureIgnoreCase))
                    {
                        newPixels[x, y] = keep.Color;
                        anyChange = true;
                    }
                    else
                    {
                        newPixels[x, y] = c;
                    }
                }
            }

            if (!anyChange)
            {
                MessageBox.Show("No pixels were assigned to the colour you’re merging. (Tip: Ensure the canvas is quantised to active colours, or pick the other selection order.)");
                return;
            }

            // One clean undo step for the whole merge
            Canvas.ApplyPixelsWithUndo(newPixels);

            // Remove the dropped colour from Active, keep 'keep' symbol/code as-is
            _active.RemoveAll(a => a.Code.Equals(drop.Code, StringComparison.InvariantCultureIgnoreCase));
            ActiveList.Items.Refresh();

            UpdateActivePaletteCounts();
            MarkDirty();
        }

        private void TestBuildPattern_Click(object sender, RoutedEventArgs e)
        {
            if (_active == null || _active.Count == 0)
            {
                MessageBox.Show("Please create the active palette first.");
                return;
            }

            var pattern = PatternBuilder.FromCanvas(Canvas, _active);

            // temporary symbol assignment
            string symbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            int i = 0;
            foreach (var col in _active)
                pattern.SymbolMap[col.Code] = symbols[i++ % symbols.Length];

            var opts = new PatternExportOptions
            {
                IncludeColourGrid = true,
                IncludeSymbolGrid = true,
                OutputPath = "PatternTest.pdf"
            };

            PatternExporter.ToPdf(pattern, opts);
            MessageBox.Show("Pattern exported to PatternTest.pdf");
        }



        // --- Unsaved-changes support ---
        private bool _isDirty = false;

        private void MarkDirty()
        {
            if (!_isDirty)
            {
                _isDirty = true;
                if (!Title.EndsWith("*")) Title += " *";
            }
        }
        private void MarkClean()
        {
            _isDirty = false;
            Title = Title.Replace(" *", "");
        }

        // Centralized save dialog you can reuse
        private bool SaveProjectInteractive()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save PixelStitch Project",
                Filter = "PixelStitch Project (*.pxsproj)|*.pxsproj",
                FileName = "project.pxsproj"
            };
            if (dlg.ShowDialog() != true) return false;

            try
            {
                var pixelLayer = Canvas.ExportPixelLayer();
                byte[] pixelPng = EncodePng(pixelLayer);

                // (If you already embedded the reference image and canvas opacity, keep that code here too)
                string? refBase64 = null;
                var refBmp = Canvas.GetReferenceImage();
                if (refBmp != null) refBase64 = Convert.ToBase64String(EncodePng(refBmp));

                var proj = new PixelStitchProject
                {
                    Width = Canvas.WidthPixels,
                    Height = Canvas.HeightPixels,
                    ReferenceOpacity = RefOpacitySlider.Value,
                    ReferenceVisible = ShowRefCheck.IsChecked == true,
                    PixelLayerPngBase64 = Convert.ToBase64String(pixelPng),
                    ReferencePngBase64 = refBase64,
                    ActivePalette = _active.Select(a => new PaletteEntry { Code = a.Code, Symbol = a.Symbol }).ToList(),
                    // CanvasOpacity = CanvasOpacitySlider.Value, // (include if you added this property)
                };

                var json = System.Text.Json.JsonSerializer.Serialize(
                    proj,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                );
                System.IO.File.WriteAllText(dlg.FileName, json);

                MarkClean();
                MessageBox.Show("Project saved.");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save project: " + ex.Message);
                return false;
            }
        }

        // Show prompt if dirty; returns true if it's OK to continue ("No" or "Yes" with successful save)
        private bool ConfirmDiscardIfDirty()
        {
            if (!_isDirty) return true;

            var choice = MessageBox.Show(
                "You have unsaved changes. Save before creating a new canvas?",
                "Unsaved changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning
            );

            if (choice == MessageBoxResult.Cancel) return false;
            if (choice == MessageBoxResult.No) return true;
            if (choice == MessageBoxResult.Yes) return SaveProjectInteractive();

            return false;
        }


        private void Canvas_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            int oldZ = (int)ZoomSlider.Value;
            int newZ = oldZ + (e.Delta > 0 ? 1 : -1);
            newZ = Math.Max((int)ZoomSlider.Minimum, Math.Min((int)ZoomSlider.Maximum, newZ));
            if (newZ == oldZ) return;

            var sv = CanvasScroll;
            var posCanvas = e.GetPosition(Canvas);
            var posViewport = e.GetPosition(sv);

            double px = posCanvas.X / oldZ;
            double py = posCanvas.Y / oldZ;

            ZoomSlider.Value = newZ;        // triggers Canvas.SetZoom via your existing handler
            sv.UpdateLayout();

            double targetCanvasX = px * newZ;
            double targetCanvasY = py * newZ;

            double newOffsetX = targetCanvasX - posViewport.X;
            double newOffsetY = targetCanvasY - posViewport.Y;

            newOffsetX = Math.Max(0, Math.Min(newOffsetX, sv.ExtentWidth - sv.ViewportWidth));
            newOffsetY = Math.Max(0, Math.Min(newOffsetY, sv.ExtentHeight - sv.ViewportHeight));

            sv.ScrollToHorizontalOffset(newOffsetX);
            sv.ScrollToVerticalOffset(newOffsetY);

            e.Handled = true;
        }

        private void Canvas_MouseDown_Pan(object? sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.MiddleButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                _isPanning = true;
                _panStart = e.GetPosition(CanvasScroll);
                _panStartH = CanvasScroll.HorizontalOffset;
                _panStartV = CanvasScroll.VerticalOffset;
                Canvas.CaptureMouse();
                Cursor = Cursors.SizeAll;
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove_Pan(object? sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isPanning && e.MiddleButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var now = e.GetPosition(CanvasScroll);
                var dx = now.X - _panStart.X;
                var dy = now.Y - _panStart.Y;

                CanvasScroll.ScrollToHorizontalOffset(_panStartH - dx);
                CanvasScroll.ScrollToVerticalOffset(_panStartV - dy);
                e.Handled = true;
            }
        }

        private void Canvas_MouseUp_Pan(object? sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isPanning && e.ChangedButton == System.Windows.Input.MouseButton.Middle)
            {
                _isPanning = false;
                Canvas.ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }


        private void UpdateActivePaletteCounts()
        {
            if (_active == null || _active.Count == 0) return;

            foreach (var a in _active) a.Count = 0;

            int w = Canvas.WidthPixels;
            int h = Canvas.HeightPixels;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = Canvas.GetPixel(x, y);
                    if (c.A == 0) continue;
                    var nearest = _active.OrderBy(a =>
                    {
                        int dr = c.R - a.Color.R, dg = c.G - a.Color.G, db = c.B - a.Color.B;
                        return dr * dr + dg * dg + db * db;
                    }).First();
                    nearest.Count++;
                }
            }

            ActiveList.Items.Refresh();
        }


        private void LoadPalette()
        {
            try
            {
                var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "dmc_palette.csv");
                if (!File.Exists(path)) { MessageBox.Show($"dmc_palette.csv not found at: {path}"); return; }
                _allDmc = File.ReadAllLines(path).Skip(1)
                              .Select(line => DmcColor.ParseCsv(line))
                              .Where(c => c != null).Select(c => c!)
                              .OrderBy(c => c.Code, StringComparer.InvariantCultureIgnoreCase).ToList();
                PaletteList.ItemsSource = _allDmc;
            }
            catch (Exception ex) { MessageBox.Show("Failed to load palette: " + ex.Message); }
        }

        private void PaletteFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var q = PaletteFilterBox.Text.Trim().ToLowerInvariant();
            PaletteList.ItemsSource = string.IsNullOrEmpty(q) ? _allDmc
                : _allDmc.Where(c => c.Code.ToLowerInvariant().Contains(q) || c.Name.ToLowerInvariant().Contains(q));
        }

        private void ResetFilter_Click(object sender, RoutedEventArgs e) => PaletteFilterBox.Text = "";

        private void PaletteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PaletteList.SelectedItem is DmcColor dc)
            {
                _activeColour = dc;
                (FindName("ActiveBrush") as SolidColorBrush)!.Color = dc.Color;
                ActiveLabel.Text = $"DMC {dc.Code} {dc.Name}";
                Canvas.SetActiveColor(dc.Color);
            }
        }

        private void ActiveList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActiveList.SelectedItem is DmcColor dc)
            {
                _activeColour = dc;
                (FindName("ActiveBrush") as SolidColorBrush)!.Color = dc.Color;
                ActiveLabel.Text = $"DMC {dc.Code} {dc.Name}";
                Canvas.SetActiveColor(dc.Color);
            }
        }

        private void EnsureSymbol(DmcColor item)
        {
            if (!string.IsNullOrEmpty(item.Symbol)) return;
            var used = new HashSet<string>(_active.Where(a => !string.IsNullOrEmpty(a.Symbol)).Select(a => a.Symbol!), StringComparer.Ordinal);
            foreach (var s in SymbolChoices) if (!used.Contains(s)) { item.Symbol = s; return; }
            int i = 1; while (true) { var sym = i.ToString(); if (!used.Contains(sym)) { item.Symbol = sym; return; } i++; }
        }

        private void AssignSymbolsSequential()
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            int idx = 0;
            foreach (var a in _active)
            {
                string sym;
                do { sym = idx < SymbolChoices.Count ? SymbolChoices[idx] : (idx - SymbolChoices.Count + 1).ToString(); idx++; }
                while (used.Contains(sym));
                a.Symbol = sym; used.Add(sym);
            }
        }

        private void AddSelectedToActive_Click(object sender, RoutedEventArgs e)
        {
            if (PaletteList.SelectedItem is DmcColor dc && !_active.Any(a => string.Equals(a.Code, dc.Code, StringComparison.InvariantCultureIgnoreCase)))
            {
                var add = dc.Clone();
                EnsureSymbol(add);
                _active.Add(add);
                ActiveList.Items.Refresh();
                Canvas.StrokeCommitted += (s, e) => { MarkDirty(); UpdateActivePaletteCounts(); };

            }
        }
        private void RemoveSelectedActive_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveList.SelectedItem is DmcColor dc)
            {
                _active.RemoveAll(a => string.Equals(a.Code, dc.Code, StringComparison.InvariantCultureIgnoreCase));
                ActiveList.Items.Refresh();
            }
        }
        private void ClearActive_Click(object sender, RoutedEventArgs e)
        {
            _active.Clear();
            ActiveList.Items.Refresh();
        }

        private void ActiveFromCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(MaxColoursBox.Text, out int n) || n < 1) { MessageBox.Show("Enter a valid Max Colours."); return; }
            var top = GetTopDmcFromCanvas(n).ToList();
            _active = top.Select(dc => dc.Clone()).ToList();
            foreach (var a in _active) EnsureSymbol(a);
            ActiveList.ItemsSource = _active;
            ActiveList.Items.Refresh();
            UpdateActivePaletteCounts();
            Canvas.StrokeCommitted += (s, e) => { MarkDirty(); UpdateActivePaletteCounts(); };

        }

        private IEnumerable<DmcColor> GetTopDmcFromCanvas(int n)
        {
            var counts = new Dictionary<string, (DmcColor colour, int count)>(StringComparer.InvariantCultureIgnoreCase);
            int w = Canvas.WidthPixels, h = Canvas.HeightPixels;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = Canvas.GetPixel(x, y);
                    if (c.A == 0) continue;
                    var nearest = NearestInPalette(c, _allDmc);
                    if (!counts.TryGetValue(nearest.Code, out var tup)) counts[nearest.Code] = (nearest, 1);
                    else counts[nearest.Code] = (tup.colour, tup.count + 1);
                }
            return counts.Values.OrderByDescending(v => v.count).Take(n).Select(v => v.colour);
        }

        private void NewCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDiscardIfDirty()) return;

            if (!int.TryParse(WidthBox.Text, out int w) || !int.TryParse(HeightBox.Text, out int h) ||
                w < 1 || h < 1 || w > 2048 || h > 2048)
            {
                MessageBox.Show("Enter valid size (1..2048).");
                return;
            }

            Canvas.NewCanvas(w, h);
            Canvas.ClearReferenceImage();
            ShowRefCheck.IsChecked = true;
            RefOpacitySlider.Value = 0.6;
            MarkClean(); // fresh document is clean
        }


        private void LoadRefImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog() { Title = "Select reference image", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
            if (dlg.ShowDialog() == true)
            {
                try { var bmp = new BitmapImage(); bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.UriSource = new Uri(dlg.FileName); bmp.EndInit(); bmp.Freeze(); Canvas.SetReferenceImage(bmp); }
                catch (Exception ex) { MessageBox.Show("Failed to load image: " + ex.Message); }
            }
            Canvas.StrokeCommitted += (s, e) => { MarkDirty(); UpdateActivePaletteCounts(); };
        }

        private void ClearCanvas_Click(object sender, RoutedEventArgs e)
        {
            Canvas.ClearWithUndo();
            UpdateActivePaletteCounts();
            MarkDirty();
        }


        private void SavePng_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog() { Title = "Save Pixel Layer as PNG", Filter = "PNG Image|*.png", FileName = "pixelstitch.png" };
            if (dlg.ShowDialog() == true)
            {
                try { var wb = Canvas.ExportPixelLayer(); using var fs = File.OpenWrite(dlg.FileName); var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(wb)); enc.Save(fs); }
                catch (Exception ex) { MessageBox.Show("Failed to save: " + ex.Message); }
            }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Canvas != null) Canvas.SetZoom((int)e.NewValue); }

        private void Canvas_ColorPicked(System.Windows.Media.Color color)
        {
            ActiveLabel.Text = $"Picked RGB ({color.R},{color.G},{color.B})";
            (FindName("ActiveBrush") as SolidColorBrush)!.Color = color;
        }

        private void Undo_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e) => Canvas.Undo();
        private void Redo_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e) => Canvas.Redo();
        private void Undo_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e) => e.CanExecute = Canvas?.CanUndo ?? false;
        private void Redo_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e) => e.CanExecute = Canvas?.CanRedo ?? false;

        private static byte[] EncodePng(BitmapSource src)
        { var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(src)); using var ms = new MemoryStream(); enc.Save(ms); return ms.ToArray(); }
        private static BitmapImage DecodePngToBitmapImage(byte[] png)
        { var bmp = new BitmapImage(); bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.StreamSource = new MemoryStream(png); bmp.EndInit(); bmp.Freeze(); return bmp; }
        private static WriteableBitmap DecodePngToWriteable(byte[] png)
        { var bi = DecodePngToBitmapImage(png); return new WriteableBitmap(new FormatConvertedBitmap(bi, PixelFormats.Pbgra32, null, 0)); }
        private static System.Windows.Media.Color[,] ColorsFromBitmap(BitmapSource src, int w, int h)
        {
            if (src.PixelWidth != w || src.PixelHeight != h)
            { src = new TransformedBitmap(src, new ScaleTransform((double)w / src.PixelWidth, (double)h / src.PixelHeight)); RenderOptions.SetBitmapScalingMode(src, BitmapScalingMode.NearestNeighbor); }
            var conv = new FormatConvertedBitmap(src, PixelFormats.Pbgra32, null, 0);
            int stride = w * 4; byte[] buf = new byte[h * stride]; conv.CopyPixels(buf, stride, 0);
            var data = new System.Windows.Media.Color[w, h];
            for (int y = 0; y < h; y++)
            {
                int row = y * stride;
                for (int x = 0; x < w; x++)
                {
                    int i = row + x * 4; byte b = buf[i + 0], g = buf[i + 1], r = buf[i + 2], a = buf[i + 3];
                    data[x, y] = a == 0 ? Colors.Transparent : System.Windows.Media.Color.FromArgb(255, r, g, b);
                }
            }
            return data;
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Title = "Save PixelStitch Project", Filter = "PixelStitch Project (*.pxsproj)|*.pxsproj", FileName = "project.pxsproj" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var pixelLayer = Canvas.ExportPixelLayer();
                byte[] pixelPng = EncodePng(pixelLayer);

                // NEW: grab reference image if present
                string? refBase64 = null;
                var refBmp = Canvas.GetReferenceImage();
                if (refBmp != null)
                {
                    var refBytes = EncodePng(refBmp);
                    refBase64 = Convert.ToBase64String(refBytes);
                }

                var proj = new PixelStitchProject
                {
                    Width = Canvas.WidthPixels,
                    Height = Canvas.HeightPixels,
                    ReferenceOpacity = RefOpacitySlider.Value,
                    ReferenceVisible = ShowRefCheck.IsChecked == true,
                    PixelLayerPngBase64 = Convert.ToBase64String(pixelPng),
                    ReferencePngBase64 = refBase64, // << embed it
                    ActivePalette = _active.Select(a => new PaletteEntry { Code = a.Code, Symbol = a.Symbol }).ToList(),
                    CanvasOpacity = CanvasOpacitySlider.Value

                };

                var json = System.Text.Json.JsonSerializer.Serialize(proj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);
                MessageBox.Show("Project saved.");
            }
            catch (Exception ex) { MessageBox.Show("Failed to save project: " + ex.Message); }
        }

        private void LoadProject_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Open PixelStitch Project", Filter = "PixelStitch Project (*.pxsproj)|*.pxsproj" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var proj = System.Text.Json.JsonSerializer.Deserialize<PixelStitchProject>(json);
                if (proj == null) throw new Exception("Invalid project.");
                WidthBox.Text = proj.Width.ToString(); HeightBox.Text = proj.Height.ToString();
                Canvas.NewCanvas(proj.Width, proj.Height);
                var pixelPng = Convert.FromBase64String(proj.PixelLayerPngBase64);
                var wb = DecodePngToWriteable(pixelPng);
                var colors = ColorsFromBitmap(wb, proj.Width, proj.Height);
                Canvas.ReplacePixels(colors);
                ShowRefCheck.IsChecked = proj.ReferenceVisible; RefOpacitySlider.Value = proj.ReferenceOpacity; CanvasOpacitySlider.Value = proj.CanvasOpacity;


                _active.Clear();
                if (proj.ActivePalette != null)
                {
                    foreach (var entry in proj.ActivePalette)
                    {
                        var d = _allDmc.FirstOrDefault(x => string.Equals(x.Code, entry.Code, StringComparison.InvariantCultureIgnoreCase));
                        if (d != null)
                        {
                            var clone = d.Clone();
                            clone.Symbol = entry.Symbol;
                            _active.Add(clone);
                        }
                    }
                }
                // Restore reference image if embedded
                if (!string.IsNullOrEmpty(proj.ReferencePngBase64))
                {
                    try
                    {
                        var refBytes = Convert.FromBase64String(proj.ReferencePngBase64);
                        var refImg = DecodePngToBitmapImage(refBytes); // you already have this helper
                        Canvas.SetReferenceImage(refImg);
                    }
                    catch { /* ignore bad/old files gracefully */ }
                }

                ActiveList.ItemsSource = _active;
                ActiveList.Items.Refresh();

                MessageBox.Show("Project loaded.");
            }
            catch (Exception ex) { MessageBox.Show("Failed to load project: " + ex.Message); }
        }

        public static DmcColor NearestInPalette(System.Windows.Media.Color src, IList<DmcColor> palette)
        {
            int best = int.MaxValue; DmcColor bestC = palette[0];
            for (int i = 0; i < palette.Count; i++)
            {
                var c = palette[i].Color;
                int dr = src.R - c.R, dg = src.G - c.G, db = src.B - c.B;
                int d = dr * dr + dg * dg + db * db;
                if (d < best) { best = d; bestC = palette[i]; }
            }
            return bestC;
        }

        private void ImportPngToCanvas_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Import Image onto Pixel Canvas", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.UriSource = new Uri(dlg.FileName); bmp.EndInit(); bmp.Freeze();
                int w = Canvas.WidthPixels, h = Canvas.HeightPixels;
                var colors = ColorsFromBitmap(bmp, w, h);
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        var c = colors[x, y];
                        colors[x, y] = c.A == 0 ? Colors.Transparent : NearestInPalette(c, _allDmc).Color;
                    }
                Canvas.ApplyPixelsWithUndo(colors);
                UpdateActivePaletteCounts();
                MarkDirty();
                MessageBox.Show("PNG imported to canvas (quantised to DMC).");

            }
            catch (Exception ex) { MessageBox.Show("Failed to import image: " + ex.Message); }
        }

        private void ReduceColours_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(MaxColoursBox.Text, out int n) || n < 1) { MessageBox.Show("Enter a valid Max Colours."); return; }
            List<DmcColor> targetPalette;
            if (UseActiveForReduce.IsChecked == true && _active.Count > 0) targetPalette = _active.Take(n).ToList();
            else targetPalette = GetTopDmcFromCanvas(n).ToList();

            int w = Canvas.WidthPixels, h = Canvas.HeightPixels;
            var newPixels = new System.Windows.Media.Color[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = Canvas.GetPixel(x, y);
                    if (c.A == 0) { newPixels[x, y] = Colors.Transparent; continue; }
                    var nearest = NearestInPalette(c, targetPalette);
                    newPixels[x, y] = nearest.Color;
                }
            Canvas.ApplyPixelsWithUndo(newPixels);
            UpdateActivePaletteCounts();
            MarkDirty();
            MessageBox.Show($"Reduced to {targetPalette.Count} colours.");

        }

        private void ReassignSymbols_Click(object sender, RoutedEventArgs e)
        {
            AssignSymbolsSequential();
            ActiveList.Items.Refresh();
            Canvas.StrokeCommitted += (s, e) => { MarkDirty(); UpdateActivePaletteCounts(); };

        }

        private void SymbolCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActiveList?.ItemsSource == null) return;

            // Enforce uniqueness: if duplicates, assign first free symbol
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in _active) if (!string.IsNullOrEmpty(a.Symbol)) used.Add(a.Symbol);
            foreach (var a in _active)
            {
                if (string.IsNullOrEmpty(a.Symbol)) continue;
                int count = _active.Count(x => x.Symbol == a.Symbol);
                if (count > 1)
                {
                    foreach (var s in SymbolChoices)
                    {
                        if (!used.Contains(s))
                        {
                            used.Add(s);
                            a.Symbol = s;
                            break;
                        }
                    }
                }
            }
            ActiveList.Items.Refresh();
            Canvas.StrokeCommitted += (s, e) => { MarkDirty(); UpdateActivePaletteCounts(); };

        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isDirty) return;
            if (!ConfirmDiscardIfDirty())
            {
                e.Cancel = true;
            }
            base.OnClosing(e);
        }

    }
}
