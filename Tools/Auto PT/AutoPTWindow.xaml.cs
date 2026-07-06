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
        public bool IsCancelled { get; private set; } = true;

        public AutoPTWindow(List<string> layers)
        {
            InitializeComponent();
            cmbLayers.ItemsSource = layers;
            if (layers.Count > 0)
                cmbLayers.SelectedIndex = 0;
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (cmbLayers.SelectedItem == null)
            {
                MessageBox.Show("Please select a layer.");
                return;
            }

            if (!double.TryParse(txtHighPoint.Text, out double hp))
            {
                MessageBox.Show("Invalid High Point value.");
                return;
            }
            if (!double.TryParse(txtLowPoint.Text, out double lp))
            {
                MessageBox.Show("Invalid Low Point value.");
                return;
            }
            if (!double.TryParse(txtChairSpacing.Text, out double cs) || cs <= 0)
            {
                MessageBox.Show("Invalid Chair Spacing value.");
                return;
            }

            SelectedLayer = cmbLayers.SelectedItem.ToString();
            HighPoint = hp;
            LowPoint = lp;
            ChairSpacing = cs;
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
