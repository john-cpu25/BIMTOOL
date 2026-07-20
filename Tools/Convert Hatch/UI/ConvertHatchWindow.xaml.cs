using System;
using System.Windows;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.ConvertHatch.UI
{
    public partial class ConvertHatchWindow : Window
    {
        private ConvertHatchEventHandler _handler;
        private ExternalEvent _exEvent;

        public ConvertHatchWindow(ConvertHatchEventHandler handler, ExternalEvent exEvent)
        {
            InitializeComponent();
            _handler = handler;
            _exEvent = exEvent;
        }

        private void btnExportHatch_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
            sfd.Filter = "JSON files (*.json)|*.json";
            sfd.Title = "Save Filled Regions Data";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _handler.SelectedPath = sfd.FileName;
                _handler.ActionToRun = ConvertHatchAction.ExportJson;
                this.Hide(); // Ẩn form để người dùng quét chuột
                _exEvent.Raise();
            }
        }

        private void btnImportHatch_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "JSON files (*.json)|*.json";
            ofd.Title = "Select Filled Regions JSON";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _handler.SelectedPath = ofd.FileName;
                _handler.ActionToRun = ConvertHatchAction.ImportJson;
                this.Hide(); // Ẩn form để người dùng quét chuột
                _exEvent.Raise();
            }
        }

        private void btnBatchExport_Click(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục để lưu các file .pat";
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _handler.SelectedPath = fbd.SelectedPath;
                    _handler.ActionToRun = ConvertHatchAction.ExportPat;
                    this.Hide(); // Ẩn form
                    _exEvent.Raise();
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _exEvent?.Dispose();
            _exEvent = null;
            _handler = null;
            base.OnClosed(e);
        }
    }
}
