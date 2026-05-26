using System.Collections.Generic;

namespace RincoNhan.Tools.Filter.Models
{
    public class FilterLibrary
    {
        public bool IsDarkMode { get; set; } = true;
        public List<SavedFilter> Filters { get; set; } = new List<SavedFilter>();
    }
}
