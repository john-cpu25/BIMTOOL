using System.Windows;
using Autodesk.Revit.UI;
using RincoNhan.Tools.Auto_Dim_Grid.ViewModels;

namespace RincoNhan.Tools.Auto_Dim_Grid.UI
{
    public partial class AutoDimGridWindow : Window
    {
        private ExternalEvent _dimGridEvent;
        private ExternalEvent _dimWallEvent;
        private ExternalEvent _dimMultiWallsEvent;

        public AutoDimGridWindow(AutoDimGridViewModel viewModel, ExternalEvent dimGridEvent, ExternalEvent dimWallEvent, ExternalEvent dimMultiWallsEvent)
        {
            InitializeComponent();
            DataContext = viewModel;
            _dimGridEvent = dimGridEvent;
            _dimWallEvent = dimWallEvent;
            _dimMultiWallsEvent = dimMultiWallsEvent;
        }

        private void BtnPickPointGrid_Click(object sender, RoutedEventArgs e)
        {
            _dimGridEvent.Raise();
        }

        private void BtnPickPointWall_Click(object sender, RoutedEventArgs e)
        {
            _dimWallEvent.Raise();
        }

        private void BtnPickMultiWalls_Click(object sender, RoutedEventArgs e)
        {
            AutoDimMultiWallsEventHandler.ActionType = "DrawLine";
            _dimMultiWallsEvent.Raise();
        }

        private void BtnPickMultiWallsSelection_Click(object sender, RoutedEventArgs e)
        {
            AutoDimMultiWallsEventHandler.ActionType = "SelectWalls";
            _dimMultiWallsEvent.Raise();
        }
    }
}
