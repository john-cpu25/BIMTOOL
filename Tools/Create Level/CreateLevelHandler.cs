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

                    // Get existing levels for duplicate checking
                    var existingLevels = new FilteredElementCollector(doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .ToList();

                    // Optionally delete existing levels first
                    if (DeleteExistingLevels && existingLevels.Count > 0)
                    {
                        // Cannot delete all levels — Revit requires at least one.
                        // We create new levels first, then delete old ones.
                        var oldIds = existingLevels.Select(l => l.Id).ToList();

                        foreach (var levelData in LevelsToCreate)
                        {
                            double elevationFeet = MmToFeet(levelData.Elevation);
                            Level newLevel = Level.Create(doc, elevationFeet);
                            newLevel.Name = GetUniqueName(doc, levelData.Name);
                            created++;
                        }

                        // Now delete old levels
                        int deleted = 0;
                        foreach (var id in oldIds)
                        {
                            try
                            {
                                doc.Delete(id);
                                deleted++;
                            }
                            catch
                            {
                                // Some levels may not be deletable (e.g., referenced by views)
                            }
                        }
                        messages.Add($"Deleted {deleted}/{oldIds.Count} existing levels.");
                    }
                    else
                    {
                        // Create levels, skipping duplicates by name
                        var existingNames = existingLevels.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

                        foreach (var levelData in LevelsToCreate)
                        {
                            if (existingNames.Contains(levelData.Name))
                            {
                                skipped++;
                                continue;
                            }

                            double elevationFeet = MmToFeet(levelData.Elevation);
                            Level newLevel = Level.Create(doc, elevationFeet);
                            newLevel.Name = levelData.Name;
                            created++;
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
        public double FloorHeight { get; set; }  // chiều cao tầng (mm)
        public double Elevation { get; set; }     // cao trình tuyệt đối (mm), tính cộng dồn
        public bool IsSelected { get; set; } = true;
    }
}
