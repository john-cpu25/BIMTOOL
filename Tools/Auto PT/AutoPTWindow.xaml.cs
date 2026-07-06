using System.Collections.Generic;
using System.Windows;

namespace RincoNhan.Tools.AutoPT
{
    public partial class AutoPTWindow : Window
    {
        public string SelectedLayer { get; private set; }
        public double HighPoint { get; private set; }
        public double LowPoint { get; private set; }
        public double ChairSpacing { get; private set; }
        public bool GeneratePTRinco { get; private set; }
        public bool GeneratePTMarker { get; private set; }
        public bool IsCancelled { get; private set; } = true;

        public AutoPTWindow(string selectedLayer)
        {
            InitializeComponent();
            SelectedLayer = selectedLayer;
            txtSelectedLayer.Text = selectedLayer;
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            GeneratePTRinco = chkGeneratePTRinco.IsChecked ?? false;
            GeneratePTMarker = false;
            IsCancelled = false;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsCancelled = true;
            this.Close();
        }
    }
}
