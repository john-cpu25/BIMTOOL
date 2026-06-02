using System.Windows;

namespace RincoNhan.Tools.ExportFamilyData.UI
{
    public partial class FamilyTypeDataPreviewWindow : Window
    {
        public FamilyTypeDataPreviewWindow(object viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            // Wire up the close action from view model if needed, or handle window close when execute finishes
            if (viewModel is ViewModels.FamilyTypeDataPreviewViewModel vm)
            {
                var originalExecute = vm.ExecuteAction;
                vm.ExecuteAction = () =>
                {
                    originalExecute?.Invoke();
                    this.Close();
                };

                var originalCancel = vm.CancelAction;
                vm.CancelAction = () =>
                {
                    originalCancel?.Invoke();
                    this.Close();
                };
            }
        }
    }
}
