using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.Connection.ViewModels
{
    public class ConnectionTypeItem
    {
        public StructuralConnectionHandlerType Type { get; set; }
        public string Name => Type.Name;
        public string ImagePath { get; set; }
    }

    public class ParameterItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class ConnectionViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private readonly ExternalEvent _externalEvent;
        private readonly ConnectionEventHandler _handler;

        public ObservableCollection<ConnectionTypeItem> ConnectionTypes { get; set; }
        public ObservableCollection<ParameterItem> ConnectionParameters { get; set; }

        private ConnectionTypeItem _selectedConnectionType;
        public ConnectionTypeItem SelectedConnectionType
        {
            get => _selectedConnectionType;
            set
            {
                _selectedConnectionType = value;
                OnPropertyChanged();
                LoadParameters();
            }
        }

        public ICommand CreateConnectionCommand { get; }

        public ConnectionViewModel(UIDocument uidoc, ExternalEvent externalEvent, ConnectionEventHandler handler)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            _externalEvent = externalEvent;
            _handler = handler;

            ConnectionTypes = new ObservableCollection<ConnectionTypeItem>();
            ConnectionParameters = new ObservableCollection<ParameterItem>();
            LoadConnectionTypes();

            CreateConnectionCommand = new RelayCommand(ExecuteCreateConnection, CanCreateConnection);
        }

        private void LoadConnectionTypes()
        {
            var types = new FilteredElementCollector(_doc)
                .OfClass(typeof(StructuralConnectionHandlerType))
                .Cast<StructuralConnectionHandlerType>()
                .OrderBy(t => t.Name)
                .ToList();

            string resourcesDir = null;
            try
            {
                resourcesDir = RincoNhan.Core.RibbonManager.ResourcesPath;
            }
            catch { /* Safe fallback */ }

            foreach (var type in types)
            {
                string expectedImagePath = null;
                if (!string.IsNullOrEmpty(resourcesDir))
                {
                    // Clean name for file path - thay khoảng trắng bằng _
                    string cleanName = type.Name.Replace(" ", "_");
                    cleanName = string.Join("_", cleanName.Split(System.IO.Path.GetInvalidFileNameChars()));
                    expectedImagePath = System.IO.Path.Combine(resourcesDir, $"{cleanName}.png");
                    
                    if (!System.IO.File.Exists(expectedImagePath))
                    {
                        expectedImagePath = System.IO.Path.Combine(resourcesDir, "SteelConnectionDefault.png");
                    }
                }

                ConnectionTypes.Add(new ConnectionTypeItem 
                { 
                    Type = type,
                    ImagePath = expectedImagePath
                });
            }

            if (ConnectionTypes.Any())
            {
                SelectedConnectionType = ConnectionTypes.First();
            }
        }

        private void LoadParameters()
        {
            ConnectionParameters.Clear();

            if (_selectedConnectionType == null) return;

            var type = _selectedConnectionType.Type;

            // Thêm thông tin cơ bản
            ConnectionParameters.Add(new ParameterItem { Name = "Type Name", Value = type.Name });
            ConnectionParameters.Add(new ParameterItem { Name = "Family Name", Value = type.FamilyName });
            ConnectionParameters.Add(new ParameterItem { Name = "Element Id", Value = type.Id.ToString() });

            // Lấy tất cả parameters có thể đọc được từ Type
            try
            {
                foreach (Parameter param in type.Parameters)
                {
                    if (param.Definition == null) continue;
                    
                    string paramName = param.Definition.Name;
                    string paramValue = GetParameterValue(param);

                    // Bỏ qua các parameter rỗng hoặc trùng
                    if (string.IsNullOrWhiteSpace(paramValue) || paramValue == "-1") continue;
                    if (paramName == "Type Name" || paramName == "Family Name") continue;

                    ConnectionParameters.Add(new ParameterItem
                    {
                        Name = paramName,
                        Value = paramValue
                    });
                }
            }
            catch { /* Some parameters may not be readable */ }
        }

        private string GetParameterValue(Parameter param)
        {
            if (!param.HasValue) return "";

            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.AsString() ?? "";
                case StorageType.Integer:
                    return param.AsInteger().ToString();
                case StorageType.Double:
                    // Convert internal units to display units
                    double val = param.AsDouble();
                    if (System.Math.Abs(val) < 1e-10) return "0";
#if REVIT2022_OR_GREATER
                    if (param.Definition.GetDataType() == SpecTypeId.Length)
                    {
                        val = val * 304.8; // feet to mm
                        return $"{val:F1} mm";
                    }
                    if (param.Definition.GetDataType() == SpecTypeId.Angle)
                    {
                        val = val * 180 / System.Math.PI; // radians to degrees
                        return $"{val:F1}°";
                    }
#else
#pragma warning disable CS0618 // Type or member is obsolete
                    if (param.Definition.ParameterType == ParameterType.Length)
                    {
                        val = val * 304.8; // feet to mm
                        return $"{val:F1} mm";
                    }
                    if (param.Definition.ParameterType == ParameterType.Angle)
                    {
                        val = val * 180 / System.Math.PI; // radians to degrees
                        return $"{val:F1}°";
                    }
#pragma warning restore CS0618
#endif
                    return $"{val:F4}";
                case StorageType.ElementId:
                    ElementId id = param.AsElementId();
                    if (id == ElementId.InvalidElementId) return "";
                    Element elem = _doc.GetElement(id);
                    return elem != null ? elem.Name : id.ToString();
                default:
                    return "";
            }
        }

        private bool CanCreateConnection(object obj)
        {
            return SelectedConnectionType != null;
        }

        private void ExecuteCreateConnection(object obj)
        {
            var typeId = SelectedConnectionType.Type.Id;

            _handler.Action = (app) =>
            {
                ConnectionLogic.CreateBeamToBeamConnection(app.ActiveUIDocument, typeId);
            };
            _externalEvent.Raise();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
