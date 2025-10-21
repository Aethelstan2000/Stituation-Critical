using Microsoft.Win32;
using StituationCritical.Services;
using StituationCritical.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StituationCritical
{
    public class StituationCriticalProject
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

    //public class PaletteEntry
    //{
    //    public string Code { get; set; } = "";
    //    public string? Symbol { get; set; }
    //}

    public class PaletteEntry : INotifyPropertyChanged
    {
        public string Code { get; set; }        // DMC code
        public string Name { get; set; }        // DMC name
        public System.Windows.Media.Color Color { get; set; }
        public string? Symbol { get; set; }

        private bool _isLocked;
        public bool IsLocked
        {
            get => _isLocked;
            set { if (_isLocked != value) { _isLocked = value; OnPropertyChanged(nameof(IsLocked)); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    public class LockIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool locked = value is bool b && b;
            return locked ? "\uE72E" : "\uE785"; // Locked : Unlocked
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class LockColourConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool locked = value is bool b && b;
            return locked ? Brushes.Gold : Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class LockTipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool locked = value is bool b && b;
            return locked ? "Locked – will not be removed when reducing" : "Unlocked – can be removed when reducing";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
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
        //private string outputPath = "PatternTest.pdf";
        private string docTitle = "New";
        private string DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
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
            // Apply AppSettings defaults
            try {
                var s = AppSettings.Current;
                if (WidthBox != null) WidthBox.Value = s.DefaultCanvasWidth;
                if (HeightBox != null) HeightBox.Value = s.DefaultCanvasHeight;
                if (RefOpacitySlider != null) RefOpacitySlider.Value = s.ReferenceOpacity;
                if (CanvasOpacitySlider != null) CanvasOpacitySlider.Value = s.PixelLayerOpacity;
                if (ShowRefCheck != null) ShowRefCheck.IsChecked = s.ReferenceVisible;
            } catch {}
            this.Loaded += MainWindow_Loaded_ApplyDefaultCanvasSize;
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
        private void MainWindow_Loaded_ApplyDefaultCanvasSize(object? sender, RoutedEventArgs e)
        {
            var s = AppSettings.Current;

            int w = Math.Max(1, s.DefaultCanvasWidth);
            int h = Math.Max(1, s.DefaultCanvasHeight);

            // Ensure the boxes reflect saved prefs
            if (WidthBox != null) WidthBox.Text = w.ToString();
            if (HeightBox != null) HeightBox.Text = h.ToString();

            // Call the same resize routine the button uses
            Canvas.ResizeCanvas(w, h, _resizeAnchor);

            UpdateActivePaletteCounts();
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

        private void ExportPattern_Click(object sender, RoutedEventArgs e)
        {
            if (_active == null || _active.Count == 0)
            {
                MessageBox.Show("Please create the active palette first.");
                return;
            }

            var pattern = PatternBuilder.FromCanvas(Canvas, _active);

            /// ToDo Implement options settings here
            var opts = new PatternExportOptions
            {
                IncludeColourGrid = true,
                IncludeSymbolGrid = true,
                IncludeStitchTypeGrid = true,
                IncludeTrueSizeGrid = true,
                CellSizemm = AppSettings.Current.GridCellMm,
                ClothCount =AppSettings.Current.ClothCount,
                Dpi = AppSettings.Current.ExportDpi,
                MarginCm = 1.5,
                HeaderCm = 1.2,
                OverlapCells = 1,
                ShowRulers = true,
                OutputPath = docTitle
            };

            // Let the user pick a save path
            var dlg = new SaveFileDialog
            {
                Title = "Export Pattern",
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = docTitle+".pdf",
                //InitialDirectory = DefaultDirectory,
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dlg.ShowDialog(this) != true)
                return; // user cancelled

            opts.OutputPath = dlg.FileName;
            DefaultDirectory = dlg.FileName;
            try
            {
                PatternExporter.ToPdf(pattern, opts);
                MessageBox.Show($"Exported:\n{opts.OutputPath}", "Export Complete",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            //PatternExporter.ToPdf(pattern, opts);
            //MessageBox.Show("Pattern exported to PatternTest.pdf");
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
                Title = "Save StituationCritical Project",
                Filter = "StituationCritical Project (*.pxsproj)|*.pxsproj",
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

                var proj = new StituationCriticalProject
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
            if (choice == MessageBoxResult.Yes) SaveProject();

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
            ActiveFromCanvas();
            return;
            if (!int.TryParse(MaxColoursBox.Text, out int n) || n < 1) { MessageBox.Show("Enter a valid Max Colours."); return; }
            var top = GetTopDmcFromCanvas(n).ToList();
            _active = top.Select(dc => dc.Clone()).ToList();
            foreach (var a in _active) EnsureSymbol(a);
            ActiveList.ItemsSource = _active;
            ActiveList.Items.Refresh();
            UpdateActivePaletteCounts();
            Canvas.StrokeCommitted += (s, e) => { MarkDirty(); UpdateActivePaletteCounts(); };

        }

        private string NextFreeSymbol(HashSet<string> used)
        {
            foreach (var s in SymbolChoices)
                if (used.Add(s))
                    return s;

            int i = 1;
            while (!used.Add($"S{i}")) i++;
            return $"S{i}";
        }

        private void EnsureSymbol(DmcColor entry, HashSet<string> used)
        {
            if (!string.IsNullOrWhiteSpace(entry.Symbol))
            {
                used.Add(entry.Symbol);
                return;
            }
            entry.Symbol = NextFreeSymbol(used);
        }

        private void ActiveFromCanvas()
        {
            if (!int.TryParse(MaxColoursBox.Text, out int n) || n < 1)
            {
                MessageBox.Show("Enter a valid Max Colours.");
                return;
            }

            // 1) Keep locked colours first
            var locked = (_active ?? new List<DmcColor>()).Where(a => a.IsLocked).ToList();
            if (locked.Count > n) n = locked.Count;

            var final = new List<DmcColor>(n);
            var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var d in locked)
                if (!string.IsNullOrWhiteSpace(d.Code) && seenCodes.Add(d.Code))
                    final.Add(d);

            // 2) Fill remaining from canvas Top-N (skip duplicates)
            var ranked = GetTopDmcFromCanvas(n * 3);
            foreach (var d in ranked)
            {
                if (final.Count >= n) break;
                if (!string.IsNullOrWhiteSpace(d.Code) && seenCodes.Add(d.Code))
                    final.Add(d);
            }

            // 3) Build _active clones, preserving lock flags and symbols for locked
            var lockedCodes = new HashSet<string>(locked.Select(x => x.Code), StringComparer.OrdinalIgnoreCase);
            var usedSymbols = new HashSet<string>(StringComparer.Ordinal);

            _active = final.Select(dc =>
            {
                var clone = dc.Clone();                   // your existing clone method
                clone.IsLocked = lockedCodes.Contains(dc.Code);

                if (clone.IsLocked && !string.IsNullOrWhiteSpace(dc.Symbol))
                    clone.Symbol = dc.Symbol;             // preserve existing
                else
                    clone.Symbol = null;                  // force reassign for unlocked

                return clone;
            }).ToList();

            // 4) Assign unique symbols (no reuse)
            foreach (var a in _active.Where(a => !string.IsNullOrWhiteSpace(a.Symbol)))
                usedSymbols.Add(a.Symbol!);

            foreach (var a in _active.Where(a => string.IsNullOrWhiteSpace(a.Symbol)))
                EnsureSymbol(a, usedSymbols);

            // 5) Finish up
            ActiveList.ItemsSource = _active;
            ActiveList.Items.Refresh();
            UpdateActivePaletteCounts();

            // Avoid duplicate event subscription
            Canvas.StrokeCommitted -= Canvas_StrokeCommitted_ForCounts;
            Canvas.StrokeCommitted += Canvas_StrokeCommitted_ForCounts;
        }

        private void Canvas_StrokeCommitted_ForCounts(object? s, EventArgs e)
        {
            MarkDirty();
            UpdateActivePaletteCounts();
        }


        /// <summary>
        /// Imports full colour palette when importing to canvas
        /// </summary>
        private void ActiveFromCanvasAll()
        {
            var counts = new Dictionary<string, (DmcColor dmc, int count)>(StringComparer.OrdinalIgnoreCase);

            for (int y = 0; y < Canvas.HeightPixels; y++)
            {
                for (int x = 0; x < Canvas.WidthPixels; x++)
                {
                    var c = Canvas.GetPixel(x, y);
                    if (c.A == 0) continue;

                    var dmc = MainWindow.NearestInPalette(c, _allDmc);
                    if (dmc == null) continue;

                    if (!counts.TryGetValue(dmc.Code, out var entry))
                        counts[dmc.Code] = (dmc, 1);
                    else
                        counts[dmc.Code] = (entry.dmc, entry.count + 1);
                }
            }

            if (counts.Count == 0)
            {
                MessageBox.Show("No colours found on the canvas.");
                return;
            }

            // Order by usage, clone for Active, ensure symbols
            _active = counts
                .OrderByDescending(kv => kv.Value.count)
                .Select(kv => kv.Value.dmc.Clone())
                .ToList();

            foreach (var a in _active) EnsureSymbol(a);

            ActiveList.ItemsSource = _active;
            ActiveList.Items.Refresh();

            UpdateActivePaletteCounts();
            MarkDirty();
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
        
        private List<DmcColor> BuildTopPalettePreservingLocks(int n)
        {
            if (n < 1) n = 1;

            // 1) Collect locked colours from current active palette
            var locked = _active.Where(d => d.IsLocked).ToList();

            // Never drop a locked colour
            if (locked.Count > n)
                n = locked.Count;

            // 2) Start final list with locked (dedup by Code)
            var final = new List<DmcColor>(n);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var d in locked)
                if (!string.IsNullOrWhiteSpace(d.Code) && seen.Add(d.Code))
                    final.Add(d);

            // 3) Get ranked candidates from canvas (your existing function)
            //    Overshoot a little to skip duplicates/locked naturally
            var ranked = GetTopDmcFromCanvas(n * 3);

            foreach (var d in ranked)
            {
                if (final.Count >= n) break;
                if (!string.IsNullOrWhiteSpace(d.Code) && seen.Add(d.Code))
                    final.Add(d);
            }

            // 4) Safety backfill from full DMC list if needed (very rare)
            if (final.Count < n)
            {
                foreach (var d in _allDmc)
                {
                    if (final.Count >= n) break;
                    if (!string.IsNullOrWhiteSpace(d.Code) && seen.Add(d.Code))
                        final.Add(d);
                }
            }

            // 5) Preserve IsLocked flags on output
            var lockedCodes = new HashSet<string>(locked.Select(x => x.Code), StringComparer.OrdinalIgnoreCase);
            foreach (var d in final)
                d.IsLocked = lockedCodes.Contains(d.Code);

            return final;
        }


        private void NewCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDiscardIfDirty()) return;
            var s = AppSettings.Current; // fetch current settings cleanly
            int w = Math.Max(1, s.DefaultCanvasWidth);
            int h = Math.Max(1, s.DefaultCanvasHeight);

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
            var dlg = new SaveFileDialog() { Title = "Save Pixel Layer as PNG", Filter = "PNG Image|*.png", FileName = "StituationCritical.png" };
            if (dlg.ShowDialog() == true)
            {
                try { var wb = Canvas.ExportPixelLayer(); using var fs = File.OpenWrite(dlg.FileName); var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(wb)); enc.Save(fs); }
                catch (Exception ex) { MessageBox.Show("Failed to save: " + ex.Message); }
            }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Canvas != null) Canvas.SetZoom((int)e.NewValue); }

        private void Canvas_ColorPicked(System.Windows.Media.Color color)
        {
            // Try to find the picked colour first in the Active Palette, then in the full DMC list
            var match = _active.FirstOrDefault(a => a.Color == color)
                     ?? _allDmc.FirstOrDefault(a => a.Color == color);

            if (match != null)
            {
                SetActiveColour(match);           // update UI + canvas paint colour
                ActiveList.SelectedItem = match;  // highlight in Active Palette list
                ActiveList.ScrollIntoView(match);
            }
            else
            {
                // Not an exact DMC shade – still use it
                Canvas.SetActiveColor(color);
                (FindName("ActiveBrush") as SolidColorBrush)!.Color = color;
                ActiveLabel.Text = $"RGB {color.R},{color.G},{color.B}";
            }
        }


        private void SetActiveColour(DmcColor dmc)
        {
            _activeColour = dmc;

            // Update the canvas paint colour used for new strokes
            Canvas.SetActiveColor(dmc.Color);

            // Update the swatch + label
            (FindName("ActiveBrush") as SolidColorBrush)!.Color = dmc.Color;
            ActiveLabel.Text = $"DMC {dmc.Code} — {dmc.Name}";
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
            SaveProject();
        }

        private void SaveProject()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save Stituation Critical Project",
                Filter = "Stituation Critical Project (*.pxsproj)|*.pxsproj",
                FileName = docTitle + ".pxsproj"
            };
            if (dlg.ShowDialog() != true) return;

            var path = dlg.FileName;
            var tmpPath = path + ".tmp";
            var bakPath = path + ".bak";

            try
            {
                // --- Gather data ---
                var pixelLayer = Canvas.ExportPixelLayer();
                byte[] pixelPng = EncodePng(pixelLayer);

                string? refBase64 = null;
                var refBmp = Canvas.GetReferenceImage();
                if (refBmp != null)
                {
                    var refBytes = EncodePng(refBmp);
                    refBase64 = Convert.ToBase64String(refBytes);
                }

                var proj = new StituationCriticalProject
                {
                    Width = Canvas.WidthPixels,
                    Height = Canvas.HeightPixels,
                    ReferenceOpacity = RefOpacitySlider.Value,
                    ReferenceVisible = ShowRefCheck.IsChecked == true,
                    PixelLayerPngBase64 = Convert.ToBase64String(pixelPng),
                    ReferencePngBase64 = refBase64,
                    ActivePalette = _active.Select(a => new PaletteEntry
                    {
                        Code = a.Code,
                        Symbol = a.Symbol,
                        // IsLocked = a.IsLocked   // ← uncomment to persist locks
                    }).ToList(),
                    CanvasOpacity = CanvasOpacitySlider.Value
                };

                var json = System.Text.Json.JsonSerializer.Serialize(
                    proj,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                // Ensure target directory exists
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // --- Atomic write: write to temp, then replace/move ---
                File.WriteAllText(tmpPath, json);  // UTF-8 without BOM by default

                if (File.Exists(path))
                {
                    // Atomic swap with backup; .bak can help diagnose if needed
                    File.Replace(tmpPath, path, bakPath);
                }
                else
                {
                    File.Move(tmpPath, path);
                }

                MessageBox.Show("Project saved.");

                docTitle = Path.GetFileNameWithoutExtension(path);
                Title = docTitle;
                MarkClean();
            }
            catch (Exception ex)
            {
                // Cleanup temp file if something failed mid-save
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { /* ignore */ }
                MessageBox.Show("Failed to save project: " + ex.Message);
            }
        }

        private void LoadProject_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Open StituationCritical Project", Filter = "StituationCritical Project (*.pxsproj)|*.pxsproj" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var proj = System.Text.Json.JsonSerializer.Deserialize<StituationCriticalProject>(json);
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
                UpdateActivePaletteCounts();
                MessageBox.Show("Project loaded.");
                docTitle = Path.GetFileNameWithoutExtension(dlg.FileName);
                Title = docTitle;
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

        private void ImportToCanvas_Click(object sender, RoutedEventArgs e)
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
                ActiveFromCanvasAll();
                UpdateActivePaletteCounts();
                MarkDirty();
                if (_active.Count > 0) SetActiveColour(_active[0]);

                //var colours = Canvas.AnalyseTopDmcColours(maxColours: 32);
                //ActivePalette.Load(colours);
                //ActivePalette.AssignSymbols();
                //UpdateActivePaletteCounts();

                MessageBox.Show("PNG imported to canvas (quantised to DMC).");

            }
            catch (Exception ex) { MessageBox.Show("Failed to import image: " + ex.Message); }
        }

        /*
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
        */
        private void ReduceColours_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(MaxColoursBox.Text, out int n) || n < 1)
            {
                MessageBox.Show("Enter a valid Max Colours.");
                return;
            }

            // 1) Collect locked colours from the active palette (requires DmcColor.IsLocked)
            var locked = _active.Where(d => d.IsLocked).ToList();

            // Ensure we never drop a locked colour
            if (locked.Count > n)
                n = locked.Count;

            // We'll build the final palette here, always starting with locked
            var targetPalette = new List<DmcColor>(n);

            // Use code (or ARGB) to dedupe
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in locked)
                if (seen.Add(d.Code))
                    targetPalette.Add(d);

            // 2) Fill remaining slots based on the user's choice
            int remaining = n - targetPalette.Count;

            if (remaining > 0)
            {
                if (UseActiveForReduce.IsChecked == true && _active.Count > 0)
                {
                    // Keep current active order, skipping already-added locked entries
                    foreach (var d in _active)
                    {
                        if (targetPalette.Count >= n) break;
                        if (seen.Add(d.Code))
                            targetPalette.Add(d);
                    }
                }
                else
                {
                    // Use your canvas-derived ranking
                    foreach (var d in GetTopDmcFromCanvas(n * 2)) // overshoot a bit to skip dupes/locked
                    {
                        if (targetPalette.Count >= n) break;
                        if (seen.Add(d.Code))
                            targetPalette.Add(d);
                    }
                }
            }

            // Safety: if we still came up short (unlikely), backfill from full list
            if (targetPalette.Count < n)
            {
                foreach (var d in _allDmc)
                {
                    if (targetPalette.Count >= n) break;
                    if (seen.Add(d.Code))
                        targetPalette.Add(d);
                }
            }

            // 3) Recolour the canvas using the final target palette
            int w = Canvas.WidthPixels, h = Canvas.HeightPixels;
            var newPixels = new System.Windows.Media.Color[w, h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = Canvas.GetPixel(x, y);
                    if (c.A == 0) { newPixels[x, y] = Colors.Transparent; continue; }
                    var nearest = NearestInPalette(c, targetPalette);
                    newPixels[x, y] = nearest.Color;
                }
            }

            Canvas.ApplyPixelsWithUndo(newPixels);

            // Optional: refresh active palette to reflect the new target (keeps lock flags)
            // Map existing lock flags by code so we don’t lose them.
            var lockedCodes = new HashSet<string>(_active.Where(a => a.IsLocked).Select(a => a.Code),
                                                  StringComparer.OrdinalIgnoreCase);
            _active = targetPalette
                .Select(d => { d.IsLocked = lockedCodes.Contains(d.Code); return d; })
                .ToList();

            ActiveList.ItemsSource = _active;
            ActiveList.Items.Refresh();

            UpdateActivePaletteCounts();
            MarkDirty();

            MessageBox.Show($"Reduced to {targetPalette.Count} colours. (Locked preserved: {locked.Count})");
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

    
        private void OpenSettings_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dlg = new StituationCritical.Settings.SettingsWindow { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                // Re-apply settings upon save
                var s = AppSettings.Current;
                if (RefOpacitySlider != null) RefOpacitySlider.Value = s.ReferenceOpacity;
                if (CanvasOpacitySlider != null) CanvasOpacitySlider.Value = s.PixelLayerOpacity;
                if (ShowRefCheck != null) ShowRefCheck.IsChecked = s.ReferenceVisible;
            }
        }
}
}
