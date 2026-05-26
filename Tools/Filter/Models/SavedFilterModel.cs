using System;
using System.Collections.Generic;

namespace RincoNhan.Tools.Filter.Models
{
    public class SavedFilter
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime SavedAt { get; set; }
        public List<SavedGroup> Groups { get; set; } = new List<SavedGroup>();
    }

    public class SavedGroup
    {
        public bool IsAndLogic { get; set; }
        public List<SavedRule> Rules { get; set; } = new List<SavedRule>();
    }

    public class SavedRule
    {
        public string CategoryName { get; set; }
        public string FamilyName { get; set; }
        public string ParameterName { get; set; }
        public string Operator { get; set; }
        public string Value { get; set; }
    }
}
