using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.JoinElements.ViewModels;

namespace RincoNhan.Tools.JoinElements
{
    public class JoinElementsExternalEventHandler : IExternalEventHandler
    {
        private ExternalEvent _externalEvent;
        public string RequestAction { get; set; }
        public MainViewModel ViewModel { get; set; }

        public JoinElementsExternalEventHandler()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        public void Raise()
        {
            _externalEvent.Raise();
        }

        public void Execute(UIApplication app)
        {
            if (ViewModel == null || app.ActiveUIDocument == null) return;

            Document doc = app.ActiveUIDocument.Document;
            View activeView = doc.ActiveView;

            try
            {
                using (Transaction trans = new Transaction(doc, "Rinco - " + RequestAction))
                {
                    trans.Start();
                    int count = 0;

                    if (RequestAction.Contains("ALL"))
                    {
                        count = ProcessAll(doc, activeView, RequestAction);
                    }
                    else if (RequestAction.Contains("PAIRS"))
                    {
                        count = ProcessPairs(doc, activeView, RequestAction);
                    }

                    trans.Commit();
                    ViewModel.SetStatus($"Finished: {count} elements processed.", true);
                }
            }
            catch (Exception ex)
            {
                ViewModel.SetStatus("Error: " + ex.Message, false);
            }
        }

        public string GetName() => "JoinElementsActionHandler";

        private int ProcessAll(Document doc, View view, string action)
        {
            int count = 0;
            var elements = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model)
                .ToList();

            foreach (var elem in elements)
            {
                if (elem.Location == null) continue;
                
                BoundingBoxXYZ bb = elem.get_BoundingBox(view);
                if (bb == null) continue;

                Outline outline = new Outline(bb.Min, bb.Max);
                BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(outline);

                var candidates = new FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType()
                    .Excluding(new List<ElementId> { elem.Id })
                    .WherePasses(filter)
                    .ToList();

                foreach (var candidate in candidates)
                {
                    if (ExecuteGeometryAction(doc, elem, candidate, action))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private int ProcessPairs(Document doc, View view, string action)
        {
            int count = 0;
            foreach (var pair in ViewModel.CategoryPairs)
            {
                if (pair.CategoryA == null || pair.CategoryB == null) continue;

                var elementsA = new FilteredElementCollector(doc, view.Id)
                    .OfCategoryId(pair.CategoryA.Id)
                    .WhereElementIsNotElementType()
                    .ToList();

                var elementsB = new FilteredElementCollector(doc, view.Id)
                    .OfCategoryId(pair.CategoryB.Id)
                    .WhereElementIsNotElementType()
                    .ToList();

                foreach (var elemA in elementsA)
                {
                    BoundingBoxXYZ bb = elemA.get_BoundingBox(view);
                    if (bb == null) continue;

                    Outline outline = new Outline(bb.Min, bb.Max);
                    BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(outline);

                    var candidatesB = new FilteredElementCollector(doc, view.Id)
                        .OfCategoryId(pair.CategoryB.Id)
                        .WhereElementIsNotElementType()
                        .Excluding(new List<ElementId> { elemA.Id })
                        .WherePasses(filter)
                        .ToList();

                    foreach (var elemB in candidatesB)
                    {
                        if (ExecuteGeometryAction(doc, elemA, elemB, action))
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        private bool ExecuteGeometryAction(Document doc, Element e1, Element e2, string action)
        {
            try
            {
                if (action.StartsWith("JOIN"))
                {
                    if (!JoinGeometryUtils.AreElementsJoined(doc, e1, e2))
                    {
                        JoinGeometryUtils.JoinGeometry(doc, e1, e2);
                        return true;
                    }
                }
                else if (action.StartsWith("UNJOIN"))
                {
                    if (JoinGeometryUtils.AreElementsJoined(doc, e1, e2))
                    {
                        JoinGeometryUtils.UnjoinGeometry(doc, e1, e2);
                        return true;
                    }
                }
                else if (action.StartsWith("SWITCH"))
                {
                    if (JoinGeometryUtils.AreElementsJoined(doc, e1, e2))
                    {
                        JoinGeometryUtils.SwitchJoinOrder(doc, e1, e2);
                        return true;
                    }
                }
            }
            catch
            {
                // Some elements cannot be joined, ignore and continue
            }
            return false;
        }
    }
}
