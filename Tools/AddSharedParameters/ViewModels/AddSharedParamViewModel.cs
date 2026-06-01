using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RincoNhan.Tools.AddSharedParameters.Models;

namespace RincoNhan.Tools.AddSharedParameters.ViewModels
{
    public partial class AddSharedParamViewModel : ObservableObject
    {
        private Document _doc;
        private Autodesk.Revit.ApplicationServices.Application _app;

        public ObservableCollection<SPDefinitionItem> Parameters { get; set; } = new ObservableCollection<SPDefinitionItem>();

        public ObservableCollection<string> ParameterGroups { get; set; } = new ObservableCollection<string>
        {
            "Data",
            "General",
            "Dimensions",
            "Identity Data",
            "Graphics",
            "Text",
            "Materials and Finishes",
            "Structural",
            "Phasing"
        };

        [ObservableProperty]
        private string _selectedGroup = "Data";

        [ObservableProperty]
        private bool _isInstance = false;

        [ObservableProperty]
        private bool _isType = true;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        public AddSharedParamViewModel(Document doc, Autodesk.Revit.ApplicationServices.Application app)
        {
            _doc = doc;
            _app = app;
            LoadSharedParameters();
        }

        private void LoadSharedParameters()
        {
            try
            {
                DefinitionFile spFile = _app.OpenSharedParameterFile();
                if (spFile == null)
                {
                    StatusMessage = "No Shared Parameter file is attached to Revit.";
                    return;
                }

                // Get existing parameter names in family to skip them
                var existingNames = new HashSet<string>();
                foreach (FamilyParameter fp in _doc.FamilyManager.Parameters)
                {
                    existingNames.Add(fp.Definition.Name);
                }

                foreach (DefinitionGroup group in spFile.Groups)
                {
                    foreach (ExternalDefinition def in group.Definitions)
                    {
                        if (existingNames.Contains(def.Name))
                            continue; // Skip already existing parameters

                        Parameters.Add(new SPDefinitionItem
                        {
                            IsSelected = false,
                            Definition = def,
                            Name = def.Name,
                            GroupName = group.Name,
#if REVIT2022_OR_GREATER
                            DataType = def.GetDataType().TypeId
#else
                            DataType = def.ParameterType.ToString()
#endif
                        });
                    }
                }

                StatusMessage = $"Loaded {Parameters.Count} available parameters from Shared Parameter file.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error loading Shared Parameters: " + ex.Message;
            }
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var p in Parameters) p.IsSelected = true;
        }

        [RelayCommand]
        private void SelectNone()
        {
            foreach (var p in Parameters) p.IsSelected = false;
        }

        [RelayCommand]
        private void AddParameters(Window window)
        {
            var selectedParams = Parameters.Where(p => p.IsSelected).ToList();
            if (!selectedParams.Any())
            {
                MessageBox.Show("Please select at least one parameter.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (Transaction t = new Transaction(_doc, "Add Shared Parameters"))
            {
                t.Start();
                int count = 0;

                foreach (var sp in selectedParams)
                {
                    try
                    {
#if REVIT2024_OR_GREATER
                        ForgeTypeId groupTypeId = GetForgeGroupTypeId(SelectedGroup);
                        _doc.FamilyManager.AddParameter(sp.Definition, groupTypeId, IsInstance);
#else
                        BuiltInParameterGroup builtInGroup = GetBuiltInGroup(SelectedGroup);
                        _doc.FamilyManager.AddParameter(sp.Definition, builtInGroup, IsInstance);
#endif
                        count++;
                    }
                    catch (Exception ex)
                    {
                        // Some parameters might fail if they conflict with built-in ones
                    }
                }

                t.Commit();
                MessageBox.Show($"Successfully added {count} parameters.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                window.Close();
            }
        }

#if !REVIT2024_OR_GREATER
        private BuiltInParameterGroup GetBuiltInGroup(string groupName)
        {
            switch (groupName)
            {
                case "Data": return BuiltInParameterGroup.PG_DATA;
                case "General": return BuiltInParameterGroup.PG_GENERAL;
                case "Dimensions": return BuiltInParameterGroup.PG_GEOMETRY;
                case "Identity Data": return BuiltInParameterGroup.PG_IDENTITY_DATA;
                case "Graphics": return BuiltInParameterGroup.PG_GRAPHICS;
                case "Text": return BuiltInParameterGroup.PG_TEXT;
                case "Materials and Finishes": return BuiltInParameterGroup.PG_MATERIALS;
                case "Structural": return BuiltInParameterGroup.PG_STRUCTURAL;
                case "Phasing": return BuiltInParameterGroup.PG_PHASING;
                default: return BuiltInParameterGroup.PG_DATA;
            }
        }
#endif

#if REVIT2024_OR_GREATER
        private ForgeTypeId GetForgeGroupTypeId(string groupName)
        {
            switch (groupName)
            {
                case "Data": return GroupTypeId.Data;
                case "General": return GroupTypeId.General;
                case "Dimensions": return GroupTypeId.Geometry;
                case "Identity Data": return GroupTypeId.IdentityData;
                case "Graphics": return GroupTypeId.Graphics;
                case "Text": return GroupTypeId.Text;
                case "Materials and Finishes": return GroupTypeId.Materials;
                case "Structural": return GroupTypeId.Structural;
                case "Phasing": return GroupTypeId.Phasing;
                default: return GroupTypeId.Data;
            }
        }
#endif
    }
}
