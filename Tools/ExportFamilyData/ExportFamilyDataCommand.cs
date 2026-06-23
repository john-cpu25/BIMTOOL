using System;
using System.IO;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.ExportFamilyData.UI;
using RincoNhan.Tools.ExportFamilyData.ViewModels;

namespace RincoNhan.Tools.ExportFamilyData
{
    [Transaction(TransactionMode.Manual)]
    public class ExportFamilyDataCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            if (!doc.IsFamilyDocument)
            {
                TaskDialog.Show("Lỗi", "Lệnh này chỉ chạy được trong môi trường Family Document (.rfa).");
                return Result.Failed;
            }

            try
            {
                // 1. Trích xuất dữ liệu
                var data = ExportDataLogic.ExtractData(doc);

                // 2. Khởi tạo ViewModel và UI
                var viewModel = new FamilyDataDebugViewModel(data, "Export Family Data Debug", "Lưu ra JSON");
                var window = new FamilyDataDebugWindow(viewModel);

                // 3. Gán hành động khi ấn nút Lưu
                viewModel.ExecuteAction = () =>
                {
                    using (var saveFileDialog = new System.Windows.Forms.SaveFileDialog())
                    {
                        saveFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                        saveFileDialog.Title = "Lưu file dữ liệu Family";
                        saveFileDialog.FileName = $"{doc.Title}_Data.json";

                        if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            string jsonString = JsonHelper.Serialize(data);
                            File.WriteAllText(saveFileDialog.FileName, jsonString);
                            TaskDialog.Show("Thành công", $"Đã xuất dữ liệu ra file:\n{saveFileDialog.FileName}");
                        }
                    }
                };

                // 4. Hiển thị cửa sổ Debug
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi", "Đã xảy ra lỗi:\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}


