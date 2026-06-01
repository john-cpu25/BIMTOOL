using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RincoNhan.Tools.ExportSharedParameters.Models;

namespace RincoNhan.Tools.ExportSharedParameters.ViewModels
{
    public partial class ExportViewModel : ObservableObject
    {
        private Document _doc;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private int _totalParameters = 0;

        public List<SharedParamInfo> ExtractedParameters { get; private set; }

        public ExportViewModel(Document doc)
        {
            _doc = doc;
            LoadParameters();
        }

        private void LoadParameters()
        {
            ExtractedParameters = new List<SharedParamInfo>();

            if (_doc.IsFamilyDocument)
            {
                foreach (FamilyParameter fp in _doc.FamilyManager.Parameters)
                {
                    if (fp.IsShared)
                    {
                        var info = new SharedParamInfo
                        {
                            Name = fp.Definition.Name,
                            Guid = fp.GUID,
#if REVIT2022_OR_GREATER
                            DataType = fp.Definition.GetDataType().TypeId,
#else
                            DataType = fp.Definition.ParameterType.ToString(),
#endif
                            DataCategory = "",
                            IsVisible = true,
                            Description = "",
                            UserModifiable = true
                        };

#if REVIT2024_OR_GREATER
                        info.Group = fp.Definition.GetGroupTypeId().TypeId;
#else
                        info.Group = fp.Definition.ParameterGroup.ToString();
#endif
                        ExtractedParameters.Add(info);
                    }
                }
            }
            else
            {
                var sharedParams = new FilteredElementCollector(_doc)
                    .OfClass(typeof(SharedParameterElement))
                    .Cast<SharedParameterElement>()
                    .ToList();

                foreach (var sp in sharedParams)
                {
                    var def = sp.GetDefinition();
                    if (def == null) continue;

                    var info = new SharedParamInfo
                    {
                        Name = def.Name,
                        Guid = sp.GuidValue,
#if REVIT2022_OR_GREATER
                        DataType = def.GetDataType().TypeId,
#else
                        DataType = def.ParameterType.ToString(),
#endif
                        DataCategory = "",
                        IsVisible = true,
                        Description = "",
                        UserModifiable = true
                    };

#if REVIT2024_OR_GREATER
                    info.Group = def.GetGroupTypeId().TypeId;
#else
                    info.Group = def.ParameterGroup.ToString();
#endif

                    ExtractedParameters.Add(info);
                }
            }

            TotalParameters = ExtractedParameters.Count;
            string docType = _doc.IsFamilyDocument ? "Family" : "Project";
            StatusMessage = $"Found {TotalParameters} Shared Parameters in {docType}.";
        }

        [RelayCommand]
        private void ExportExcel()
        {
            if (ExtractedParameters == null || !ExtractedParameters.Any())
            {
                StatusMessage = "No Shared Parameters to export.";
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel Files|*.xlsx";
                dialog.Title = "Save Shared Parameters as Excel";
                dialog.FileName = "SharedParameters.xlsx";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (var workbook = new XLWorkbook())
                        {
                            var ws = workbook.Worksheets.Add("Shared Parameters");
                            ws.Cell(1, 1).Value = "Name";
                            ws.Cell(1, 2).Value = "GUID";
                            ws.Cell(1, 3).Value = "DataType";
                            ws.Cell(1, 4).Value = "Group";

                            for (int i = 0; i < ExtractedParameters.Count; i++)
                            {
                                var p = ExtractedParameters[i];
                                ws.Cell(i + 2, 1).Value = p.Name;
                                ws.Cell(i + 2, 2).Value = p.Guid.ToString();
                                ws.Cell(i + 2, 3).Value = p.DataType;
                                ws.Cell(i + 2, 4).Value = p.Group;
                            }

                            ws.Columns().AdjustToContents();
                            workbook.SaveAs(dialog.FileName);
                        }
                        StatusMessage = "Exported to Excel successfully!";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = "Error: " + ex.Message;
                    }
                }
            }
        }

        [RelayCommand]
        private void ExportTxt()
        {
            if (ExtractedParameters == null || !ExtractedParameters.Any())
            {
                StatusMessage = "No Shared Parameters to export.";
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Text Files|*.txt";
                dialog.Title = "Save Shared Parameters File";
                dialog.FileName = "SharedParameters.txt";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("# This is a Revit shared parameter file.");
                        sb.AppendLine("# Generated by BIMTOOL");
                        sb.AppendLine("*META	VERSION	MINVERSION");
                        sb.AppendLine("META	2	1");
                        sb.AppendLine("*GROUP	ID	NAME");
                        
                        int groupId = 1;
                        var groups = ExtractedParameters.Select(p => p.Group).Distinct().ToList();
                        var groupDict = new Dictionary<string, int>();

                        foreach (var g in groups)
                        {
                            string gName = string.IsNullOrEmpty(g) ? "Exported_Group" : g.Replace("autodesk.parameter.group.", "").Replace("-1.0.0", "").Replace("PG_", "");
                            groupDict[g] = groupId;
                            sb.AppendLine($"GROUP	{groupId}	{gName}");
                            groupId++;
                        }

                        sb.AppendLine("*PARAM	GUID	NAME	DATATYPE	DATACATEGORY	GROUP	VISIBLE	DESCRIPTION	USERMODIFIABLE");
                        
                        foreach (var p in ExtractedParameters)
                        {
                            string dt = MapForgeTypeIdToLegacy(p.DataType);
                            int gId = groupDict[p.Group];
                            sb.AppendLine($"PARAM	{p.Guid}	{p.Name}	{dt}		{gId}	1		1");
                        }

                        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.Unicode);
                        StatusMessage = "Exported to TXT successfully!";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = "Error: " + ex.Message;
                    }
                }
            }
        }

        private string MapForgeTypeIdToLegacy(string forgeTypeId)
        {
            if (string.IsNullOrEmpty(forgeTypeId)) return "TEXT";
            string lower = forgeTypeId.ToLower();
            if (lower.Contains("string") || lower.Contains("text")) return "TEXT";
            if (lower.Contains("int")) return "INTEGER";
            if (lower.Contains("number")) return "NUMBER";
            if (lower.Contains("length")) return "LENGTH";
            if (lower.Contains("area")) return "AREA";
            if (lower.Contains("volume")) return "VOLUME";
            if (lower.Contains("angle")) return "ANGLE";
            if (lower.Contains("material")) return "MATERIAL";
            if (lower.Contains("yesno") || lower.Contains("boolean")) return "YESNO";
            if (lower.Contains("url")) return "URL";
            return "TEXT"; 
        }
    }
}
