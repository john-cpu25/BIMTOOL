using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Revit.Async;
using RincovitchTools.DrawingRegister;
using RincovitchTools.General.Revit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using View = Autodesk.Revit.DB.View;

namespace RincovitchTools.Step
{
  [Transaction(TransactionMode.Manual)]
  internal class Rincovitch_Step : IExternalCommand
  {
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
      try
      {
        UIApplication uiapp = commandData.Application;
        UIDocument uidoc = uiapp.ActiveUIDocument;
        Document doc = uidoc.Document;

        RevitTask.Initialize(uiapp);

        var hwndSource = HwndSource.FromHwnd(uiapp.MainWindowHandle);
        Window? revit = hwndSource.RootVisual as Window;

        string error_path = $@"C:\ProgramData\Autodesk\ApplicationPlugins\Rincovitch.bundle\ERROR\Step\{doc.Title}";
        if(!Directory.Exists(error_path))
          Directory.CreateDirectory(error_path);

        using (Transaction t = new Transaction(doc, "Step"))
        {
          t.Start();

          #region step
          var fm_step = new FilteredElementCollector(doc, doc.ActiveView.Id)
            .OfClass(typeof(FamilyInstance))
            .OfCategory(BuiltInCategory.OST_GenericAnnotation)
            .Cast<FamilyInstance>()
            .Where(x => x.Symbol.Name.Contains("Step"))
            .ToList();

          var floors = new FilteredElementCollector(doc, doc.ActiveView.Id)
            .OfClass(typeof(Floor))
            .OfCategory(BuiltInCategory.OST_Floors)
            .ToList();
          var beams = new FilteredElementCollector(doc, doc.ActiveView.Id)
            .OfClass(typeof(FamilyInstance))
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .ToList();

          floors.AddRange(beams);

          List<FamilyInstance> errors = new List<FamilyInstance>();
          foreach (var item in fm_step)
          {
            var para_name = "RL STEP";
            var value_exist = item.LookupParameter(para_name)?.AsString();
            if (!int.TryParse(value_exist, out _))
              continue;

            var step_point = (item.Location as LocationPoint).Point;
            var step_floors = GetFloorsContainingPoint(floors, step_point);
            if(step_floors.Count() > 1)
            {
              var result = step_floors.Select(x => Convert.ToInt32(x.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP).AsValueString()));
              string value = Math.Abs(result.Max() - result.Min()).ToString();
              item.LookupParameter(para_name)?.Set(value);
            }
            else
            {
              errors.Add(item);
            }
          }
          string error_file = Path.Combine(error_path, "ERROR_STEP.txt");
          if (errors.Count > 0)
          {
            File.WriteAllLines(error_file, errors.Select(x => x.UniqueId));
            StepERROR window = new StepERROR(error_file);
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Owner = revit;
            window.Show();
          }
          else
          {
            if(File.Exists(error_file))
              File.Delete(error_file);
          }
            #endregion

            t.Commit();
        }

        return Result.Succeeded;
      }
      catch (Exception ex)
      {
        System.Windows.MessageBox.Show(ex.Message);
        return Result.Failed;
      }
    }

    private List<Element> GetFloorsContainingPoint(List<Element> floors, XYZ pt)
    {
      List<Element> result = new List<Element>();
      double offset = 10.0/304.8;
      List<XYZ> testPoints = CreateOffsetPoints(pt, offset);
      try
      {
        foreach (Element floor in floors)
        {
          Options opt = new Options();
          GeometryElement geo = floor.get_Geometry(opt);

          foreach (GeometryObject obj in geo)
          {
            Solid solid = obj as Solid;
            if (solid == null || solid.Faces.Size == 0) continue;

            foreach (Face face in solid.Faces)
            {
              // Chỉ lấy mặt top
              if (!IsTopFace(face)) continue;

              foreach (XYZ tpt in testPoints)
              {
                IntersectionResult ir = face.Project(tpt);
                if (ir == null) continue;

                UV uv = ir.UVPoint;

                if (face.IsInside(uv))
                {
                  result.Add(floor);
                  goto NEXT_FLOOR;
                }
              }
            }
          }
        NEXT_FLOOR:;
        }
      }
      catch (Exception ex) 
      {
        System.Windows.MessageBox.Show(ex.Message);
      }

      return result;
    }
    private bool IsTopFace(Face face)
    {
      if(face is not PlanarFace) return false;

      PlanarFace pf = face as PlanarFace;
      if (pf == null) return false;

      XYZ normal = pf.FaceNormal;

      // Top face có normal hướng +Z
      return normal.IsAlmostEqualTo(XYZ.BasisZ);
    }

    private List<XYZ> CreateOffsetPoints(XYZ pt, double offset)
    {
      return new List<XYZ>()
    {
        pt,                                 // điểm gốc
        pt + new XYZ( offset, 0, 0),        // phải
        pt + new XYZ(-offset, 0, 0),        // trái
        pt + new XYZ(0,  offset, 0),        // lên / north
        pt + new XYZ(0, -offset, 0)         // xuống / south
    };
    }
  }
}
