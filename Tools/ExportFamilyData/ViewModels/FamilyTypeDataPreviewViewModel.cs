using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RincoNhan.Tools.ExportFamilyData.ViewModels
{
    public partial class FamilyTypeDataPreviewViewModel : ObservableObject
    {
        [ObservableProperty]
        private string titleText;

        [ObservableProperty]
        private string executeButtonText;

        [ObservableProperty]
        private ObservableCollection<FamilyTypeModel> types;
        
        [ObservableProperty]
        private ObservableCollection<ParameterModel> selectedParameters;

        private FamilyTypeModel _selectedType;
        public FamilyTypeModel SelectedType
        {
            get => _selectedType;
            set 
            { 
                SetProperty(ref _selectedType, value);
                if (value != null && value.Parameters != null)
                {
                    SelectedParameters = new ObservableCollection<ParameterModel>(value.Parameters);
                }
                else
                {
                    SelectedParameters = new ObservableCollection<ParameterModel>();
                }
            }
        }

        public Action ExecuteAction { get; set; }
        public Action CancelAction { get; set; }

        public FamilyTypeDataPreviewViewModel(FamilyTypeExportModel data, string title = "Import Family Type Data Preview", string executeBtnText = "Chạy Import")
        {
            TitleText = title;
            ExecuteButtonText = executeBtnText;

            Types = new ObservableCollection<FamilyTypeModel>(data.Types ?? new List<FamilyTypeModel>());
            
            if (Types.Count > 0)
            {
                SelectedType = Types[0];
            }
            else
            {
                SelectedParameters = new ObservableCollection<ParameterModel>();
            }
        }

        [RelayCommand]
        private void Execute()
        {
            ExecuteAction?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            CancelAction?.Invoke();
        }
    }
}
