using System.Windows;

namespace RincoNhan.Tools.AlignTags
{
    /// <summary>
    /// Interaction logic for AlignTagsWindow.xaml
    /// </summary>
    public partial class AlignTagsWindow : Window
    {
        public AlignTagsWindow(AlignTagsViewModel viewModel)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
