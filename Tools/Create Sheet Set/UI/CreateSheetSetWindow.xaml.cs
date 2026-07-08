using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RincoNhan.Tools.Create_Sheet_Set.ViewModels;

namespace RincoNhan.Tools.Create_Sheet_Set.UI
{
    public partial class CreateSheetSetWindow : Window
    {
        private List<RevisionViewModel> _revisions;

        public bool MatchAny => rbMatchAny.IsChecked == true;
        public string SetName => txtSetName.Text;

        public CreateSheetSetWindow(List<RevisionViewModel> revisions)
        {
            InitializeComponent();
            _revisions = revisions;
            lbRevisions.ItemsSource = _revisions;
        }

        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            var selectedRevisions = _revisions.Where(x => x.IsSelected).ToList();
            if (selectedRevisions.Count > 1)
            {
                rbMatchAny.IsEnabled = true;
                rbMatchAll.IsEnabled = true;
            }
            else
            {
                rbMatchAny.IsEnabled = false;
                rbMatchAll.IsEnabled = false;
                rbMatchAll.IsChecked = true;
            }

            if (selectedRevisions.Count > 0)
            {
                if (selectedRevisions.Count > 1)
                {
                    txtSetName.Text = string.Join(" & ", selectedRevisions.Select(r => r.Name));
                }
                else
                {
                    txtSetName.Text = selectedRevisions[0].Name;
                }
            }
            else
            {
                txtSetName.Text = "";
            }
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
