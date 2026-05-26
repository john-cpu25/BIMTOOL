using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RincoNhan.Core.ClashDetection;

namespace RincoNhan.Tools.ClashDetection.ViewModels
{
    public partial class ClashDetectionViewModel : ObservableObject
    {
        private ClashDetectionExternalEventHandler _handler;

        [ObservableProperty]
        private ObservableCollection<RevitLinkInstance> _links;

        [ObservableProperty]
        private RevitLinkInstance _selectedLink;

        [ObservableProperty]
        private bool _isLinkSelected = true;

        [ObservableProperty]
        private ObservableCollection<Category> _linkCategories;

        [ObservableProperty]
        private Category _selectedLinkCategory;

        [ObservableProperty]
        private ObservableCollection<ClashResult> _results = new ObservableCollection<ClashResult>();

        [ObservableProperty]
        private string _statusMessage = "Ready. Select a Link and Categories.";

        public ClashDetectionViewModel(ClashDetectionExternalEventHandler handler, Document doc)
        {
            _handler = handler;
            _handler.ViewModel = this;

            // Load Links
            var linkInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
            Links = new ObservableCollection<RevitLinkInstance>(linkInstances);
            if (Links.Count > 0) SelectedLink = Links[0];

            // Load Categories (Đơn giản hóa: Lấy một số category phổ biến)
            LoadCategories(doc);
        }

        private void LoadCategories(Document doc)
        {
            var categories = doc.Settings.Categories
                .Cast<Category>()
                .Where(c => c.CategoryType == CategoryType.Model)
                .OrderBy(c => c.Name)
                .ToList();
            
            LinkCategories = new ObservableCollection<Category>(categories);
        }

        public List<Element> GetHostElements(Document doc)
        {
            // Lấy UIDocument từ document thông qua Application (nếu có thể)
            // Đơn giản nhất là sử dụng static property nếu có hoặc truyền UIDocument vào
            // Ở đây tôi giả định có thể lấy được Selection từ ActiveUIDocument
            
            var selectionIds = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .WhereElementIsCurveDriven() // Một cách lọc sơ bộ nếu không có selection
                .ToElementIds();

            // Thực tế Revit UI API cần được gọi đúng cách. 
            // Tôi sẽ dùng cách an toàn hơn cho Revit API 2022-2026:
            return new FilteredElementCollector(doc, doc.ActiveView.Id)
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();
        }

        public void UpdateResults(List<ClashResult> newResults)
        {
            Results.Clear();
            foreach (var res in newResults) Results.Add(res);
        }

        [RelayCommand]
        private void RunCheck()
        {
            StatusMessage = "Checking...";
            _handler.RequestAction = "RUN_CHECK";
            _handler.Raise();
        }

        [RelayCommand]
        private void ShowClash(ClashResult result)
        {
            if (result == null) return;
            _handler.RequestAction = "SHOW_CLASH";
            _handler.SelectedClash = result;
            _handler.Raise();
        }
    }
}
