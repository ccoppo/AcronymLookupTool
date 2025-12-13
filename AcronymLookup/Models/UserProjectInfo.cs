using System; 

namespace AcronymLookup.Models
{
    /// <summary>
    /// holds information about a project that user has access to
    /// used for project selection and switching
    /// </summary>
    public class UserProjectInfo
    {

        public int ProjectID { get; set; }
        public string ProjectName { get; set; } = "";
        public string ProjectCode { get; set; } = "";
        public string Description { get; set; } = "";
        public string UserRole { get; set; } = "Viewer";

        public string DisplayName => $"{ProjectCode} - {ProjectName}";

        public override string ToString()
        {
            return DisplayName;
        }

    }
}