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
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
