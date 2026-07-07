using Autodesk.Revit.DB;

namespace RincoNhan.Tools.Create_Sheet_Set.ViewModels
{
    public class RevisionViewModel
    {
        public ElementId Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string Date { get; }
        public bool IsSelected { get; set; }

        public RevisionViewModel(Revision revision)
        {
            Id = revision.Id;
            Name = $"{revision.RevisionDate} - {revision.Description}";
            Description = revision.Description;
            Date = revision.RevisionDate;
            IsSelected = false;
        }
    }
}
