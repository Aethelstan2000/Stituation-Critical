
using System.Windows;
using Xceed.Wpf.Toolkit;

namespace StituationCritical.Settings
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;

        public SettingsWindow()
        {
            InitializeComponent();
            _settings = AppSettings.Current;

            // Populate controls
            WidthBox.Value = _settings.DefaultCanvasWidth;
            HeightBox.Value = _settings.DefaultCanvasHeight;
            ZoomBox.Value = _settings.DefaultZoom;

            RefOpacityBox.Value = _settings.ReferenceOpacity;
            PixelOpacityBox.Value = _settings.PixelLayerOpacity;
            RefVisibleCheck.IsChecked = _settings.ReferenceVisible;

            DpiBox.Value = _settings.ExportDpi;
            GridCellBox.Value = _settings.GridCellMm;

            // Page size
            PageSizeBox.SelectedIndex = 0;
            for (int i = 0; i < PageSizeBox.Items.Count; i++)
            {
                var item = PageSizeBox.Items[i] as System.Windows.Controls.ComboBoxItem;
                if (item != null && (string)item.Content == _settings.ExportPageSize)
                {
                    PageSizeBox.SelectedIndex = i;
                    break;
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (WidthBox.Value.HasValue) _settings.DefaultCanvasWidth = (int)WidthBox.Value.Value;
            if (HeightBox.Value.HasValue) _settings.DefaultCanvasHeight = (int)HeightBox.Value.Value;
            if (ZoomBox.Value.HasValue) _settings.DefaultZoom = ZoomBox.Value.Value;

            if (RefOpacityBox.Value.HasValue) _settings.ReferenceOpacity = RefOpacityBox.Value.Value;
            if (PixelOpacityBox.Value.HasValue) _settings.PixelLayerOpacity = PixelOpacityBox.Value.Value;
            _settings.ReferenceVisible = RefVisibleCheck.IsChecked == true;

            if (DpiBox.Value.HasValue) _settings.ExportDpi = (int)DpiBox.Value.Value;
            if (GridCellBox.Value.HasValue) _settings.GridCellMm = GridCellBox.Value.Value;

            var sel = PageSizeBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            _settings.ExportPageSize = sel != null ? (string)sel.Content : "A4";

            _settings.Save();
            this.DialogResult = true;
            this.Close();
        }
    }
}
