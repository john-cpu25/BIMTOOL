using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RincoNhan.Tools.ExportFamilyData.ViewModels
{
    public partial class FamilyDataDebugViewModel : ObservableObject
    {
        public FamilyDataModel Data { get; }
        
        [ObservableProperty]
        private string titleText;

        [ObservableProperty]
        private string executeButtonText;

        public ObservableCollection<ParameterModel> Parameters { get; }
        public ObservableCollection<ReferencePlaneModel> ReferencePlanes { get; }
        public ObservableCollection<LineModel> Lines { get; }
        public ObservableCollection<DimensionModel> Dimensions { get; }

        public Action ExecuteAction { get; set; }
        public Action CloseAction { get; set; }

        public FamilyDataDebugViewModel(FamilyDataModel data, string title, string executeText)
        {
            Data = data;
            TitleText = title;
            ExecuteButtonText = executeText;

            Parameters = new ObservableCollection<ParameterModel>(data.Parameters ?? new System.Collections.Generic.List<ParameterModel>());
            ReferencePlanes = new ObservableCollection<ReferencePlaneModel>(data.ReferencePlanes ?? new System.Collections.Generic.List<ReferencePlaneModel>());
            Lines = new ObservableCollection<LineModel>(data.Lines ?? new System.Collections.Generic.List<LineModel>());
            Dimensions = new ObservableCollection<DimensionModel>(data.Dimensions ?? new System.Collections.Generic.List<DimensionModel>());
        }

        [RelayCommand]
        private void Execute()
        {
            ExecuteAction?.Invoke();
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseAction?.Invoke();
        }
    }
}
