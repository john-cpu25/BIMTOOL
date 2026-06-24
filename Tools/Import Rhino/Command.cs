using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;

namespace RincoModeling.Tools.ImportRhino
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                Document doc = uiapp.ActiveUIDocument.Document;

                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Rhino 3D Models (*.3dm)|*.3dm";
                openFileDialog.Title = "Select a Rhino .3dm File";
                
                // Mở thư mục mặc định theo file bạn yêu cầu
                openFileDialog.InitialDirectory = @"c:\Users\Nhan\OneDrive - Rincovitch\00. Nhan\CSharp\3D\Unterlagen Schleife\3D Modell\";

                if (openFileDialog.ShowDialog() == true)
                {
                    string filePath = openFileDialog.FileName;
                    
                    ImportRhinoLogic logic = new ImportRhinoLogic();
                    int count = logic.Import3dmAsDirectShape(doc, filePath);
                    
                    if (count > 0)
                    {
                        TaskDialog.Show("Success", $"Import thành công {count} lưới hình học từ Rhino 3DM!");
                    }
                    else
                    {
                        TaskDialog.Show("Warning", "Không tìm thấy Mesh nào trong file Rhino. \n\nLưu ý: Bạn hãy mở file trong Rhino, chuyển sang chế độ nhìn Shaded (hoặc bôi đen toàn bộ gõ lệnh 'Mesh' để convert) rồi Save lại để file đính kèm Render Mesh nhé!");
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
