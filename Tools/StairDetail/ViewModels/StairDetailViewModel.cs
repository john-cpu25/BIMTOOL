using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.StairDetail.ViewModels;

public partial class StairDetailViewModel : ObservableObject
{
    [ObservableProperty]
    private string _selectedThepChu = Settings.Default.ThepChu;

    [ObservableProperty]
    private string _selectedThepPhu = Settings.Default.ThepPhu;

    [ObservableProperty]
    private string _thepChuSpacing = Settings.Default.ThepChu_Spacing;

    [ObservableProperty]
    private string _thepPhuSpacing = Settings.Default.ThepPhu_Spacing;

    [ObservableProperty]
    private bool _includeRebarLandingTop = true;

    [ObservableProperty]
    private bool _includeRebarLandingBot = false;

    public List<string> RebarTypeNames { get; }

    public StairDetailViewModel(List<Element> rebarTypes)
    {
        RebarTypeNames = new List<string>();
        foreach (var item in rebarTypes)
        {
            RebarTypeNames.Add(item.Name);
        }
    }

    /// <summary>
    /// Save current selections to static settings for next use.
    /// </summary>
    public void SaveSettings()
    {
        Settings.Default.ThepChu = SelectedThepChu;
        Settings.Default.ThepPhu = SelectedThepPhu;
        Settings.Default.ThepChu_Spacing = ThepChuSpacing;
        Settings.Default.ThepPhu_Spacing = ThepPhuSpacing;
        Settings.Default.Save();
    }
}
