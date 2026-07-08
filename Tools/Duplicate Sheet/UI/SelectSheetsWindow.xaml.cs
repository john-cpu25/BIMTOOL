using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.DuplicateSheet.UI
{
    public partial class SelectSheetsWindow : Window
    {
        public List<SheetWrapper> Sheets { get; set; }
        public List<ViewSheet> SelectedSheets { get; private set; }

        public SelectSheetsWindow(IEnumerable<ViewSheet> availableSheets)
        {
            InitializeComponent();
            
            Sheets = availableSheets
                .OrderBy(s => s.SheetNumber)
                .Select(s => new SheetWrapper(s))
                .ToList();

            lbSheets.ItemsSource = Sheets;
        }

        private void BtnCheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var sheet in Sheets)
            {
                sheet.IsSelected = true;
            }
        }

        private void BtnCheckNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var sheet in Sheets)
            {
                sheet.IsSelected = false;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            SelectedSheets = Sheets.Where(s => s.IsSelected).Select(s => s.Sheet).ToList();
            if (SelectedSheets.Count == 0)
            {
                MessageBox.Show("Please select at least one sheet.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class SheetWrapper : System.ComponentModel.INotifyPropertyChanged
    {
        public ViewSheet Sheet { get; }
        public string DisplayName => $"{Sheet.SheetNumber} - {Sheet.Name}";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public SheetWrapper(ViewSheet sheet)
        {
            Sheet = sheet;
        }
    }
}
