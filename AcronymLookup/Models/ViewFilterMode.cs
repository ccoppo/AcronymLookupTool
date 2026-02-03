namespace AcronymLookup.Models
{
    /// <summary>
    /// represents the current view filter mode for terminology lookup
    /// </summary>
    public class ViewFilterMode
    {
        public FilterType Type { get; set; }
        public int? ProjectID { get; set; } //only used when Type = SpecificProject
        public string DisplayName { get; set; } = "ALL";

        public static ViewFilterMode CreateAllMode()
        {
            return new ViewFilterMode
            {
                Type = FilterType.All,
                ProjectID = null,
                DisplayName = "ALL"
            };
        }

        public static ViewFilterMode CreatePersonalMode()
        {
            return new ViewFilterMode
            {
                Type = FilterType.PersonalOnly,
                ProjectID = null,
                DisplayName = "Personal Only"
            };
        }

        public static ViewFilterMode CreateProjectMode(int projectId, string projectCode)
        {
            return new ViewFilterMode
            {
                Type = FilterType.SpecificProject,
                ProjectID = projectId,
                DisplayName = projectCode
            };
        }
    }

    public enum FilterType
    {
        All,             
        PersonalOnly,   
        SpecificProject 
    }
}