using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StituationCritical
{
    public class PixelCanvas : FrameworkElement
    {
        private int _w = 64, _h = 64;
        private Color[,] _pixels = new Color[64, 64];
        private int _zoom = 12;
        private Color _active = Colors.Black;
        private bool _mouseDown = false;
        private WriteableBitmap? _refImage;
        public event Action<Color>? ColorPicked;
        public BitmapSource? GetReferenceImage() => _refImage;
        public event EventHandler? StrokeCommitted;

        public int WidthPixels => _w;
        public int HeightPixels => _h;

        public static readonly DependencyProperty PixelGridVisibleProperty =
            DependencyProperty.Register(nameof(PixelGridVisible), typeof(bool), typeof(PixelCanvas),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
        public bool PixelGridVisible { get => (bool)GetValue(PixelGridVisibleProperty); set => SetValue(PixelGridVisibleProperty, value); }

        public static readonly DependencyProperty ReferenceOpacityProperty =
            DependencyProperty.Register(nameof(ReferenceOpacity), typeof(double), typeof(PixelCanvas),
                new FrameworkPropertyMetadata(0.6, FrameworkPropertyMetadataOptions.AffectsRender));
        public double ReferenceOpacity { get => (double)GetValue(ReferenceOpacityProperty); set => SetValue(ReferenceOpacityProperty, value); }

        public static readonly DependencyProperty ReferenceVisibleProperty =
            DependencyProperty.Register(nameof(ReferenceVisible), typeof(bool), typeof(PixelCanvas),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
        public bool ReferenceVisible { get => (bool)GetValue(ReferenceVisibleProperty); set => SetValue(ReferenceVisibleProperty, value); }

        public static readonly DependencyProperty PixelLayerOpacityProperty =
            DependencyProperty.Register(nameof(PixelLayerOpacity), typeof(double), typeof(PixelCanvas),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
        public double PixelLayerOpacity
        {
            get => (double)GetValue(PixelLayerOpacityProperty);
            set => SetValue(PixelLayerOpacityProperty, Math.Max(0.0, Math.Min(1.0, value)));
        }

        public enum Anchor
        {
            TopLeft, Top, TopRight,
            Left, Center, Right,
            BottomLeft, Bottom, BottomRight
        }

        /// <summary>
        /// Resize canvas to (newW,newH) and anchor the old pixels inside.
        /// No scaling: pixels copy 1:1. New area is transparent.
        /// </summary>
        public void ResizeCanvas(int newW, int newH, Anchor anchor)
        {
            if (newW <= 0 || newH <= 0) return;

            var srcW = _w;
            var srcH = _h;

            // Compute top-left position of the OLD image inside the NEW canvas
            int ox = 0, oy = 0;
            int dx = Math.Max(0, newW - srcW);  // padding if expanding
            int dy = Math.Max(0, newH - srcH);

            switch (anchor)
            {
                case Anchor.TopLeft: ox = 0; oy = 0; break;
                case Anchor.Top: ox = dx / 2; oy = 0; break;
                case Anchor.TopRight: ox = dx; oy = 0; break;
                case Anchor.Left: ox = 0; oy = dy / 2; break;
                case Anchor.Center: ox = dx / 2; oy = dy / 2; break;
                case Anchor.Right: ox = dx; oy = dy / 2; break;
                case Anchor.BottomLeft: ox = 0; oy = dy; break;
                case Anchor.Bottom: ox = dx / 2; oy = dy; break;
                case Anchor.BottomRight: ox = dx; oy = dy; break;
            }

            // New pixel buffer
            var newPix = new Color[newW, newH];
            for (int y = 0; y < newH; y++)
                for (int x = 0; x < newW; x++)
                    newPix[x, y] = Colors.Transparent;

            // Source window that overlaps the new canvas
            int copyW = Math.Min(srcW, newW);
            int copyH = Math.Min(srcH, newH);

            // Source start index if we’re cropping (when old is bigger than new)
            int srcStartX = 0, srcStartY = 0;
            int dstStartX = ox, dstStartY = oy;

            if (srcW > newW)  // crop horizontally based on anchor
            {
                switch (anchor)
                {
                    case Anchor.TopRight:
                    case Anchor.Right:
                    case Anchor.BottomRight:
                        srcStartX = srcW - newW; dstStartX = 0; break;
                    case Anchor.Top:
                    case Anchor.Center:
                    case Anchor.Bottom:
                        srcStartX = (srcW - newW) / 2; dstStartX = 0; break;
                    default:
                        srcStartX = 0; dstStartX = 0; break; // left
                }
                copyW = newW;
            }

            if (srcH > newH)  // crop vertically based on anchor
            {
                switch (anchor)
                {
                    case Anchor.BottomLeft:
                    case Anchor.Bottom:
                    case Anchor.BottomRight:
                        srcStartY = srcH - newH; dstStartY = 0; break;
                    case Anchor.Left:
                    case Anchor.Center:
                    case Anchor.Right:
                        srcStartY = (srcH - newH) / 2; dstStartY = 0; break;
                    default:
                        srcStartY = 0; dstStartY = 0; break; // top
                }
                copyH = newH;
            }

            for (int y = 0; y < copyH; y++)
            {
                for (int x = 0; x < copyW; x++)
                {
                    var sX = srcStartX + x;
                    var sY = srcStartY + y;
                    var dX = dstStartX + x;
                    var dY = dstStartY + y;
                    if (sX >= 0 && sX < srcW && sY >= 0 && sY < srcH &&
                        dX >= 0 && dX < newW && dY >= 0 && dY < newH)
                    {
                        newPix[dX, dY] = _pixels[sX, sY];
                    }
                }
            }

            // Apply
            _w = newW; _h = newH;
            _pixels = newPix;
            InvalidateVisual();
        }


        public PixelCanvas()
        {
            Clear();
            Focusable = true;
            SnapsToDevicePixels = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseLeave += (_, __) => { _mouseDown = false; ReleaseMouseCapture(); };
        }

        public void NewCanvas(int w, int h)
        {
            _w = w; _h = h;
            _pixels = new Color[w, h];
            ClearHistory();
            Clear();
            InvalidateMeasure();
            InvalidateVisual();
        }

        public void SetActiveColor(Color c)
        {
            _active = c;
            InvalidateVisual();
        }


        public void SetZoom(int zoom)
        {
            _zoom = Math.Max(1, Math.Min(zoom, 80));
            InvalidateMeasure();
            InvalidateVisual();
        }

        public void SetReferenceImage(BitmapSource src)
        {
            _refImage = new WriteableBitmap(new FormatConvertedBitmap(src, PixelFormats.Pbgra32, null, 0));
            InvalidateVisual();
        }

        public void Clear()
        {
            for (int x = 0; x < _w; x++)
                for (int y = 0; y < _h; y++)
                    _pixels[x, y] = Colors.Transparent;
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize) => new Size(_w * _zoom, _h * _zoom);

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(34, 34, 34)), null, new Rect(0, 0, _w * _zoom, _h * _zoom));

            if (ReferenceVisible && _refImage != null)
            {
                var rect = new Rect(0, 0, _w * _zoom, _h * _zoom);
                dc.PushOpacity(ReferenceOpacity);
                dc.DrawImage(_refImage, rect);
                dc.Pop();
            }

            dc.PushOpacity(PixelLayerOpacity);
            for (int x = 0; x < _w; x++)
            for (int y = 0; y < _h; y++)
            {
                var c = _pixels[x, y];
                if (c.A == 0) continue;
                var r = new Rect(x * _zoom, y * _zoom, _zoom, _zoom);
                dc.DrawRectangle(new SolidColorBrush(c), null, r);
            }
            dc.Pop();

            if (PixelGridVisible && _zoom >= 6)
            {
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 1);
                for (int x = 0; x <= _w; x++) dc.DrawLine(pen, new Point(x * _zoom + 0.5, 0), new Point(x * _zoom + 0.5, _h * _zoom));
                for (int y = 0; y <= _h; y++) dc.DrawLine(pen, new Point(0, y * _zoom + 0.5), new Point(_w * _zoom, y * _zoom + 0.5));
            }
        }

        private sealed class PixelChange { public int X, Y; public Color Old, New; }
        private sealed class StrokeAction
        {
            public readonly List<PixelChange> Changes = new();
            public void Apply(Color[,] px) { foreach (var c in Changes) px[c.X, c.Y] = c.New; }
            public void Revert(Color[,] px) { foreach (var c in Changes) px[c.X, c.Y] = c.Old; }
            public bool HasChanges => Changes.Count > 0;
        }
        private readonly Stack<StrokeAction> _undo = new();
        private readonly Stack<StrokeAction> _redo = new();
        private StrokeAction? _currentStroke;
        private HashSet<ulong>? _touched;
        public bool CanUndo => _undo.Count > 0 || (_currentStroke?.HasChanges ?? false);
        public bool CanRedo => _redo.Count > 0;
        private static ulong KeyOf(int x, int y) => (ulong)((x << 21) ^ y);

        private void BeginStroke() { _currentStroke = new StrokeAction(); _touched = new HashSet<ulong>(); }
        private void CommitStroke()
        {
            bool changed = _currentStroke != null && _currentStroke.HasChanges;

            if (changed)
            {
                _undo.Push(_currentStroke!);
                _redo.Clear();
            }

            _currentStroke = null;
            _touched = null;

            // notify listeners (MainWindow) that a stroke modified the canvas
            if (changed)
                StrokeCommitted?.Invoke(this, EventArgs.Empty);
        }


        private void RecordAndPaintAt(Point p, bool erase = false)
        {
            int x = (int)(p.X / _zoom);
            int y = (int)(p.Y / _zoom);
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;

            var newColor = erase ? Colors.Transparent : _active;
            var oldColor = _pixels[x, y];
            if (newColor == oldColor) return;

            if (_currentStroke != null)
            {
                var key = KeyOf(x, y);
                if (_touched != null && !_touched.Contains(key))
                {
                    _currentStroke.Changes.Add(new PixelChange { X = x, Y = y, Old = oldColor, New = newColor });
                    _touched.Add(key);
                }
                else if (_touched != null)
                {
                    int idx = _currentStroke.Changes.FindLastIndex(c => c.X == x && c.Y == y);
                    if (idx >= 0) _currentStroke.Changes[idx].New = newColor;
                }
            }

            _pixels[x, y] = newColor;
            InvalidateVisual();
        }

        private void OnMouseDown(object? sender, MouseButtonEventArgs e)
        {
            Focus();
            _mouseDown = true;
            CaptureMouse();

            bool erase = e.ChangedButton == MouseButton.Right && Keyboard.Modifiers == ModifierKeys.Shift;

            if (e.ChangedButton == MouseButton.Right && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                var p = e.GetPosition(this);
                int x = (int)(p.X / _zoom);
                int y = (int)(p.Y / _zoom);
                if (x >= 0 && y >= 0 && x < _w && y < _h)
                {
                    var c = _pixels[x, y];
                    ColorPicked?.Invoke(c);
                }
                // Do NOT paint; cancel any stroke & release capture
                _mouseDown = false;
                ReleaseMouseCapture();
                return;
            }
            else
            {
                BeginStroke();
                RecordAndPaintAt(e.GetPosition(this), erase);
            }

        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_mouseDown) return;

            bool leftPaint = e.LeftButton == MouseButtonState.Pressed;
            bool rightErase = e.RightButton == MouseButtonState.Pressed && Keyboard.Modifiers == ModifierKeys.Shift;

            if (leftPaint || rightErase)
            {
                bool erase = rightErase;
                RecordAndPaintAt(e.GetPosition(this), erase);
            }
        }


        private void OnMouseUp(object? sender, MouseButtonEventArgs e) { _mouseDown = false; ReleaseMouseCapture(); CommitStroke(); }

        private void ClearHistory() { _undo.Clear(); _redo.Clear(); _currentStroke = null; _touched = null; }
        public void Undo() 
        { 
            if (_currentStroke != null) CommitStroke();
            if (_undo.Count == 0) return;
            var act = _undo.Pop();
            act.Revert(_pixels);
            _redo.Push(act);
            InvalidateVisual();
        }
        public void Redo() { if (_redo.Count == 0) return; var act = _redo.Pop(); act.Apply(_pixels); _undo.Push(act); InvalidateVisual(); }

        public WriteableBitmap ExportPixelLayer()
        {
            var wb = new WriteableBitmap(_w, _h, 96, 96, PixelFormats.Pbgra32, null);
            int stride = _w * 4; byte[] pixels = new byte[_h * stride];
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                var c = _pixels[x, y]; int idx = y * stride + x * 4;
                pixels[idx + 0] = c.B; pixels[idx + 1] = c.G; pixels[idx + 2] = c.R; pixels[idx + 3] = c.A == 0 ? (byte)0 : (byte)255;
            }
            wb.WritePixels(new Int32Rect(0, 0, _w, _h), pixels, stride, 0);
            return wb;
        }

        public Color GetPixel(int x, int y) => _pixels[x, y];

        public void ReplacePixels(Color[,] data, bool clearHistory = true)
        {
            if (data.GetLength(0) != _w || data.GetLength(1) != _h) throw new ArgumentException("Pixel array dimensions must match canvas.");
            for (int x = 0; x < _w; x++) for (int y = 0; y < _h; y++) _pixels[x, y] = data[x, y];
            if (clearHistory) ClearHistory(); InvalidateVisual();
        }

        public void LoadFromBitmap(BitmapSource src)
        {
            var scaled = new TransformedBitmap(src, new ScaleTransform((double)_w / src.PixelWidth, (double)_h / src.PixelHeight));
            RenderOptions.SetBitmapScalingMode(scaled, BitmapScalingMode.NearestNeighbor);
            var conv = new FormatConvertedBitmap(scaled, PixelFormats.Pbgra32, null, 0);
            int stride = _w * 4; byte[] buf = new byte[_h * stride]; conv.CopyPixels(buf, stride, 0);
            for (int y = 0; y < _h; y++)
            {
                int row = y * stride;
                for (int x = 0; x < _w; x++)
                {
                    int i = row + x * 4; byte b = buf[i + 0], g = buf[i + 1], r = buf[i + 2], a = buf[i + 3];
                    _pixels[x, y] = a == 0 ? Colors.Transparent : Color.FromArgb(255, r, g, b);
                }
            }
            ClearHistory(); InvalidateVisual();
        }

        public void ClearReferenceImage()
        {
            _refImage = null;
            InvalidateVisual();
        }

        // Apply a full pixel buffer and create ONE undo step
        public void ApplyPixelsWithUndo(Color[,] data)
        {
            if (data.GetLength(0) != _w || data.GetLength(1) != _h)
                throw new ArgumentException("Pixel array dimensions must match canvas.");

            var action = new StrokeAction();

            for (int x = 0; x < _w; x++)
                for (int y = 0; y < _h; y++)
                {
                    var oldC = _pixels[x, y];
                    var newC = data[x, y];
                    if (oldC != newC)
                        action.Changes.Add(new PixelChange { X = x, Y = y, Old = oldC, New = newC });
                }

            if (action.HasChanges)
            {
                action.Apply(_pixels);
                _undo.Push(action);
                _redo.Clear();
                InvalidateVisual();
                StrokeCommitted?.Invoke(this, EventArgs.Empty); // keep dirty flag / counts in sync
            }
        }

        // Clear canvas but keep it undoable as a single step
        public void ClearWithUndo()
        {
            var action = new StrokeAction();
            for (int x = 0; x < _w; x++)
                for (int y = 0; y < _h; y++)
                {
                    var oldC = _pixels[x, y];
                    if (oldC.A != 0)
                        action.Changes.Add(new PixelChange { X = x, Y = y, Old = oldC, New = Colors.Transparent });
                }
            if (action.HasChanges)
            {
                action.Apply(_pixels);
                _undo.Push(action);
                _redo.Clear();
                InvalidateVisual();
                StrokeCommitted?.Invoke(this, EventArgs.Empty);
            }
        }

    }
}
