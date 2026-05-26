using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.CreateLevel
{
    public class CreateLevelHandler : IExternalEventHandler
    {
        public List<LevelData> LevelsToCreate { get; set; }
        public bool DeleteExistingLevels { get; set; }
        public ElementId TemplateLevelId { get; set; }
        public Action<string> NotifyStatus { get; set; }
        public Action<string> NotifyResult { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;

            if (LevelsToCreate == null || LevelsToCreate.Count == 0)
            {
                NotifyStatus?.Invoke("No levels to create.");
                return;
            }

            using (Transaction trans = new Transaction(doc, "Create Levels from Excel"))
            {
                trans.Start();
                try
                {
                    int created = 0;
                    int skipped = 0;
                    var messages = new List<string>();

                    // Get template level
                    Level templateLevel = null;
                    if (TemplateLevelId != null)
                    {
                        templateLevel = doc.GetElement(TemplateLevelId) as Level;
                    }

                    // Get existing levels
                    var existingLevels = new FilteredElementCollector(doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .ToList();

                    if (DeleteExistingLevels && existingLevels.Count > 0)
                    {
                        var oldIds = existingLevels.Select(l => l.Id).ToList();

                        // Rename old levels to avoid name conflicts
                        int tempIdx = 0;
                        foreach (var oldLevel in existingLevels)
                        {
                            try
                            {
                                oldLevel.Name = $"__TEMP_DELETE_{tempIdx++}__";
                            }
                            catch { }
                        }

                        foreach (var levelData in LevelsToCreate)
                        {
                            double elevationFeet = MmToFeet(levelData.Elevation);
                            Level newLevel = CreateLevelByCopy(doc, templateLevel, elevationFeet, levelData.Name);
                            if (newLevel != null) created++;
                        }

                        int deleted = 0;
                        foreach (var id in oldIds)
                        {
                            try
                            {
                                doc.Delete(id);
                                deleted++;
                            }
                            catch { }
                        }
                        messages.Add($"Deleted {deleted}/{oldIds.Count} existing levels.");
                    }
                    else
                    {
                        var existingNames = existingLevels.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

                        foreach (var levelData in LevelsToCreate)
                        {
                            if (existingNames.Contains(levelData.Name))
                            {
                                skipped++;
                                continue;
                            }

                            double elevationFeet = MmToFeet(levelData.Elevation);
                            Level newLevel = CreateLevelByCopy(doc, templateLevel, elevationFeet, levelData.Name);
                            if (newLevel != null) created++;
                        }
                    }

                    trans.Commit();

                    string result = $"✓ Created {created} levels.";
                    if (skipped > 0) result += $" Skipped {skipped} (already exist).";
                    if (messages.Count > 0) result += " " + string.Join(" ", messages);

                    NotifyStatus?.Invoke(result);
                    NotifyResult?.Invoke(result);
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    NotifyStatus?.Invoke("Error: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Copy template level to create a new level at the specified elevation.
        /// This preserves all properties: LevelType, bubble visibility, line extents, etc.
        /// Does NOT create new plan views (unlike Level.Create).
        /// </summary>
        private Level CreateLevelByCopy(Document doc, Level templateLevel, double elevationFeet, string name)
        {
            if (templateLevel != null)
            {
                double templateElevation = templateLevel.Elevation;
                double offset = elevationFeet - templateElevation;
                XYZ translation = new XYZ(0, 0, offset);

                var copiedIds = ElementTransformUtils.CopyElement(doc, templateLevel.Id, translation);

                if (copiedIds != null && copiedIds.Count > 0)
                {
                    // CopyElement may copy associated views too — find the Level element
                    Level copiedLevel = null;
                    foreach (var id in copiedIds)
                    {
                        var elem = doc.GetElement(id) as Level;
                        if (elem != null)
                        {
                            copiedLevel = elem;
                            break;
                        }
                    }

                    if (copiedLevel != null)
                    {
                        copiedLevel.Name = name;
                        return copiedLevel;
                    }
                }
            }

            // Fallback: create normally if no template
            Level newLevel = Level.Create(doc, elevationFeet);
            newLevel.Name = name;
            return newLevel;
        }

        private double MmToFeet(double mm)
        {
            return mm / 304.8;
        }

        private string GetUniqueName(Document doc, string baseName)
        {
            var existingNames = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Select(l => l.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!existingNames.Contains(baseName)) return baseName;

            int suffix = 1;
            while (existingNames.Contains($"{baseName} ({suffix})"))
            {
                suffix++;
            }
            return $"{baseName} ({suffix})";
        }

        public string GetName() => "CreateLevelFromExcel";
    }

    public class LevelData
    {
        public string Name { get; set; }
        public double FloorHeight { get; set; }
        public double Elevation { get; set; }
        public bool IsSelected { get; set; } = true;
    }
}
