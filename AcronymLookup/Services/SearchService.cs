using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcronymLookup.Utilities;
using AcronymLookup.Models; 

namespace AcronymLookup.Services
{
    internal class SearchService
    {
        #region Private Fields 

        private readonly Core.DatabaseHandler _databaseHandler;
        private readonly PersonalDatabaseService _personalDatabaseService;

        #endregion

        public SearchService(Core.DatabaseHandler databaseHandler, PersonalDatabaseService personalDatabaseService)
        {
            _databaseHandler = databaseHandler ?? throw new ArgumentNullException(nameof(databaseHandler));
            _personalDatabaseService = personalDatabaseService ?? throw new ArgumentNullException(nameof(personalDatabaseService));

            Logger.Log("SearchService created"); 
        }


        #region Search Methods 

        public SearchResult Search(string searchTerm, SearchScope scope, int userId, int projectId = 0)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return SearchResult.CreateEmpty(searchTerm); 
            }

            Logger.Log($"Searching for '{searchTerm}' with scope: {scope}");

            var result = new SearchResult
            {
                SearchTerm = searchTerm,
                Scope = scope
            }; 

            switch (scope)
            {
                case SearchScope.All:
                    result = SearchAll(searchTerm, userId, projectId);
                    break;
                case SearchScope.Personal:
                    result = SearchPersonalOnly(searchTerm, userId);
                    break;
                case SearchScope.Project:
                    result = SearchProjectOnly(searchTerm, projectId);
                    break; 
            }
            Logger.Log($"Search complete: Found {result.TotalResults} results");
            return result; 
        }

        private SearchResult SearchAll(string searchTerm, int userId, int projectId)
        {
            var result = new SearchResult
            {
                SearchTerm = searchTerm,
                Scope = SearchScope.All
            }; 

            try
            {
                //search personal database first 
                var personalTerm = _personalDatabaseService.FindPersonalAbbreviation(searchTerm, userId); 
                if (personalTerm != null)
                {
                    result.PersonalResults.Add(CreateSearchResultItem(personalTerm, "Personal")); 
                }

                //Search project database 
                var projectTerm = _databaseHandler.FindAbbreviation(searchTerm); 

                if (projectTerm != null)
                {
                    result.ProjectResults.Add(CreateSearchResultItem(projectTerm, "Project")); 
                }

                Logger.Log($"Search ALL: Personal = {result.PersonalResults.Count}, Project = {result.ProjectResults.Count}"); 
            }
        }

        #endregion

        #region Helper Methods 

        /// <summary>
        /// creates a search result item with the data and the source of the abbreviation
        /// </summary>
        /// <param name="data"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        private SearchResultItem CreateSearchResultItem(AbbreviationData data, string source)
        {
            return new SearchResultItem
            {
                Abbreviation = data.Abbreviation,
                Definition = data.Definition,
                Category = data.Category,
                Notes = data.Notes,
                Source = source,
                DateAdded = data.DateAdded
            };

        }

        /// <summary>
        /// Get all results as a list prioritizing users own terms 
        /// </summary>
        /// <param name="searchResult"></param>
        /// <returns></returns>
        private static List <AbbreviationData> GetAllResultsAsList(SearchResult searchResult)
        {
            var allResults = new List<AbbreviationData>(); 

            //Add personal results first 
            foreach(var item in searchResult.PersonalResults)
            {
                allResults.Add(new AbbreviationData(
                    item.Abbreviation,
                    item.Definition,
                    item.Category,
                    item.Notes)); 
            }

            //add project results 
            foreach(var item in searchResult.ProjectResults)
            {
                allResults.Add(new AbbreviationData(
                    item.Abbreviation,
                    item.Definition,
                    item.Category,
                    item.Notes)); 
            }

            return allResults; 
        }
    }

    #region Search Data classes 

    public enum SearchScope
    {
        All, 
        Personal, 
        Project
    }

    public class SearchResult
    {
        public string SearchTerm { get; set; } = "";
        public SearchScope Scope { get; set; }
        public List<SearchResultItem> PersonalResults { get; set; } = new List<SearchResultItem>();
        public List<SearchResultItem> ProjectResults { get; set; } = new List<SearchResultItem>();

        public int TotalResults => PersonalResults.Count + ProjectResults.Count; 
        public bool HasResults => TotalResults > 0;
        public bool HasPersonalResults => PersonalResults.Count > 0; 
        public bool HasProjectResults => ProjectResults.Count > 0;

        public static SearchResult CreateEmpty(string searchTerm) {

            return new SearchResult 
            {
                SearchTerm = searchTerm, 
                Scope = SearchScope.All
            };

        }
    }

    public class SearchResultItem
    {
        public string Abbreviation { get; set; } = "";
        public string Definition { get; set; } = "";
        public string Category { get; set; } = "";
        public string Notes { get; set; } = "";

        public string Source { get; set; } = "";

        public DateTime DateAdded { get; set; }

        public override string ToString()
        {
            return $"[{Source}] {Abbreviation}: {Definition}]"; 
        }

    }
    #endregion 
}
