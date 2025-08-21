using System;
using System.Web;
using System.Windows.Controls;

namespace AcronymLookup.Models
{

    /// <summary>
    /// Represents a single abbreviation with its definition and data 
    /// This is the core data structure: center of the program
    /// </summary>
    public class AbbreviationData
    {
        #region Properties 

        /// <summary> 
        /// The abbreviation itself (such as "API")
        /// Always stored in upper case for consistency purposes
        /// </summary>
        public string Abbreviation { get; private set; }

        /// <summary> 
        /// Full Definition/meaning of the term/acronym
        /// </summary>
        public string Definition { get; private set; }

        /// <summary> 
        /// Optional category for organization
        /// </summary>
        public string Category { get; private set; }

        /// <summary>
        /// Optional user notes for additional context
        /// </summary>
        public string Notes { get; private set; }

        /// <summary> 
        /// When term was added (for tracking purposes)
        /// </summary>
        public DateTime DateAdded { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new abbreviation entry with validation
        /// </summary>
        /// <param name="abbreviation">The abbreviation itself (will be cleaned and uppercased</param>
        /// <param name="definition">The definition (required attribute) </param>
        /// <param name="category">Optional category</param>
        /// <param name="notes">Optional notes</param>
        public AbbreviationData(string abbreviation, string definition, string category = "", string notes = "")
        {
            // security: validate and clean all input 
            if (string.IsNullOrWhiteSpace(abbreviation))
            {
                throw new ArgumentException("Abbreviation cannot be empty", nameof(abbreviation));
            }

            if (string.IsNullOrWhiteSpace(definition))
            {
                throw new ArgumentException("Definition cannot be empty", nameof(definition));
            }

            //Clean and standardize the abbreviation
            Abbreviation = CleanAbbreviation(abbreviation);

            //Clean and store other properties 
            Definition = CleanText(definition);
            Category = CleanText(category);
            Notes = CleanText(notes);
            DateAdded = DateTime.Now;
        }

        #endregion

        #region Helper Methods 
        /// <summary>
        /// Cleans and standardizes abbreviation text 
        /// SECURITY: Removes potentially harmful characters
        /// </summary>
        private static string CleanAbbreviation(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            
            //remove whitespace and convert to uppercase 
            string cleaned = text.Trim().ToUpper();

            var allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-_";
            var result = ""; 

            foreach (char c in cleaned)
            {
                if (allowedChars.Contains(c))
                    result += c; 
            }

            return result;
        }

        /// <summary>
        /// Cleans general text fields 
        /// SECURITY: removes potentially harmful content
        /// </summary>
        private static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) 
                return string.Empty; 

            //basic cleaning: remove extra whitespace
            return text.Trim().Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        }

        #endregion

        #region Public Methods 

        /// <summary>
        /// Checks if this abbreviation matches the search term 
        /// Case-insensitive matching with multiple
        /// </summary>
        public bool Matches(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return false;

            string cleanedSearch = CleanAbbreviation(searchTerm);

            //exact match (most compn) 
            if (Abbreviation.Equals(cleanedSearch, StringComparison.OrdinalIgnoreCase))
                return true;

            if (Abbreviation.Contains(cleanedSearch, StringComparison.OrdinalIgnoreCase))
                return true;

            return false; 
        }

        /// <summary>
        /// Updates the notes for this abbreviation 
        /// Used when user adds notes through the UI
        /// </summary>
        public void UpdateNotes(string newNotes)
        {
            Notes = CleanText(newNotes); 
        }

        /// <summary>
        /// Checks if this abbreviaiton has user notes 
        /// Used to show the notes icon in the bubble 
        /// </summary>
        public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

        /// <summary> 
        /// Creates a user friendly display string
        /// </summary>
        public override string ToString()
        {
            return $"{Abbreviation}: {Definition}";
        }

        #endregion 
    }
}
