using System;
using System.Collections.Generic;
using System.IO; 
using System.Linq;
using AcronymLookup.Models;
using AcronymLookup.Utilities; 

namespace AcronymLookup.Core
{
    /// <summary>
    ///  Safely parses and searches CSV files containing abbreviation data 
    ///  SECURITY FOCUS: All input is validated and sanitized 
    /// </summary>
    public class CsvParser
    {
        #region Private Fields 

        private readonly Dictionary<string, AbbreviationData> _abbreviations;
        private string _currentFilePath;
        private DateTime _lastLoadTime;

        #endregion

        #region Properties 

        /// <summary> 
        /// Number of abbreviations currently loaded 
        /// </summary>
        public int Count => _abbreviations.Count;

        /// <summary> 
        /// Path to the currently loaded CSV file
        /// </summary>
        public string CurrentFilePath => _currentFilePath ?? "No file loaded";

        /// <summary> 
        /// When the current file was last loaded
        /// </summary>
        public DateTime LastLoadTime => _lastLoadTime;

        /// <summary> 
        /// All loaded abbreviations (read-only access)
        /// </summary>
        public IReadOnlyCollection<AbbreviationData> AllAbbreviations => _abbreviations.Values;

        #endregion

        #region Contstructor
        
        /// <summary>
        /// Creates a new CSV parser
        /// </summary>
        public CsvParser()
        {
            _abbreviations = new Dictionary<string, AbbreviationData>(StringComparer.OrdinalIgnoreCase);
            _currentFilePath = string.Empty;
            _lastLoadTime = DateTime.MinValue;
        }

        #endregion

        #region Public Methods 

        /// <summary> 
        /// Loads abbreviation from a CSV file with full validation 
        /// SECURITY: Validates file path and content
        /// </summary>
        /// <param name="filePath">Path to the CSV file</param>
        /// <returns>True if loaded successfully, false otherwise</returns>
        public bool LoadFromFile(string filePath)
        {
            try
            {
                //SECURITY: Validate file path 
                if (!IsValidFilePath(filePath))
                {
                    Logger.Log($"Csv Parser: Invalid file path: {filePath}");
                    return false; 
                }

                //SECURITY: Check if file exists and is readable 
                if (!File.Exists(filePath))
                {
                    Logger.Log($"CSV Parer: File not found: {filePath}");
                    return false; 
                }

                //Clear existing data 
                _abbreviations.Clear();

                //Read and parse the file
                string[] lines = File.ReadAllLines(filePath);
                int successCount = 0;
                int errorCount = 0;

                Logger.Log($"Csv Parser: Loading abbreviations from: {filePath}");

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    //Skip empty lines and comments 
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    //skip header row if detected 
                    if (i == 0 && IsHeaderRow(line))
                    {
                        Logger.Log("Header row detected and skipped");
                        continue;
                    }

                    //Parse the line 
                    var abbreviationData = ParseCsvLine(line, i + 1);
                    if (abbreviationData != null)
                    {
                        //use abbreviationsas key for fast lookup 
                        _abbreviations[abbreviationData.Abbreviation] = abbreviationData;
                        successCount++;
                    }
                    else
                    {
                        errorCount++;
                    }
                }

                //Update tracking information 
                _currentFilePath = filePath;
                _lastLoadTime = DateTime.Now;

                Logger.Log($"Loaded {successCount} abbreviations successfully");
                if (errorCount > 0)
                    Logger.Log($"Skipped {errorCount} invalid lines");

                return successCount > 0; 
            }
            catch (Exception ex)
            {
                Logger.Log($"Error loading CSV file: {ex.Message}");
                return false; 
            }
        }

        /// <summary> 
        /// Finds an abbreviation by search term 
        /// Fast dictionary lookup with fallback strategy 
        /// </summary>
        /// <param name="searchTerm">The term to search for</param>
        /// <returns>Matching abbreviation data or null if not found</returns>
        public AbbreviationData? FindAbbreviation(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return null;

            //Clean the search term 
            string cleanedTerm = searchTerm.Trim().ToUpper(); 

            //Strategy 1: Direct dictionary lookup (fastest) 
            if(_abbreviations.TryGetValue(cleanedTerm, out AbbreviationData? directMatch))
            {
                return directMatch; 
            }

            // Strategy 2: Use the abbreviations own matching logic 
            foreach (var abbrev in _abbreviations.Values)
            {
                if (abbrev.Matches(searchTerm))
                    return abbrev;
            }

            return null; 
        }

        /// <summary> 
        /// Searches for abbreviations containing the search term 
        /// Returns multiple matches for partial searches 
        /// </summary>
        /// <param name="searchTerm">The term to search for</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <returns>List of matching abbreviations</returns>
        public List<AbbreviationData> SearchAbbreviations(string searchTerm, int maxResults = 10)
        {
            var results = new List<AbbreviationData>();

            if (string.IsNullOrWhiteSpace(searchTerm))
                return results; 

            foreach (var abbrev in _abbreviations.Values)
            {
                if (abbrev.Matches(searchTerm))
                {
                    results.Add(abbrev);
                    if (results.Count >= maxResults)
                        break;
                }
            }

            return results; 
        }

        /// <summary> 
        /// Reloads the current CSV file 
        /// Useful for refreshing data after external changes 
        /// </summary>
        /// <returns> True if reloaded successfully </returns>
        public bool Reload()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
                return false;

            return LoadFromFile(_currentFilePath); 
        }

        #endregion

        #region Private Helper Methods 
        /// <summary> 
        /// SECURITY: Validates that the file is safe to use 
        /// </summary>
        private static bool IsValidFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false; 

            try
            {
                //check for invalid characters 
                char[] invalidChars = Path.GetInvalidPathChars();
                if (filePath.Any(c => invalidChars.Contains(c)))
                    return false;

                // must be a .csv file 
                string extension = Path.GetExtension(filePath).ToLower();
                return extension == ".csv"; 
            }catch
            {
                return false; 
            }
        }

        /// <summary>
        /// detects if a line is a header row
        /// </summary>
        private static bool IsHeaderRow(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string[] parts = ParseCsvRow(line); 
            if (parts.Length >= 2)
            {
                string firstColumn = parts[0].Trim().ToLower();
                return firstColumn == "abbreviation" ||
                    firstColumn == "abbrev" ||
                    firstColumn == "acronym" ||
                    firstColumn == "short"; 
            }
            return false; 
        }

        /// <summary> 
        /// Parses a single CSV line into an AbbreviaitonData object 
        /// SECURITY: Validates all data before creating object
        /// </summary>
        private static AbbreviationData? ParseCsvLine(string line, int lineNumber)
        {
            try
            {
                string[] parts = ParseCsvRow(line); 

                //Must have at least abbreviaiton and definition
                if (parts.Length < 2)
                {
                    Logger.Log($"Line {lineNumber}: Not enough columns (need at least 2)");
                    return null; 
                }

                string abbreviation = parts[0];
                string definition = parts[1];
                string category = parts.Length > 2 ? parts[2] : "";
                string notes = parts.Length > 3 ? parts[3] : ""; 

                //validate required fields 
                if (string.IsNullOrWhiteSpace(abbreviation) || string.IsNullOrWhiteSpace(definition))
                {
                    Logger.Log($"Line {lineNumber}: Missing abbreviaiton or definition");
                    return null; 
                }

                //create and return the abbreviation data 
                return new AbbreviationData(abbreviation, definition, category, notes); 
            }catch (Exception ex)
            {
                Logger.Log($"Line {lineNumber}: Parse error - {ex.Message}");
                return null; 
            }
        }

        /// <summary> 
        /// Parses a CSV row handling quotes and commas 
        /// SECURITY: Safe parsing that cant execute code 
        /// </summary>
        private static string[] ParseCsvRow(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            string currentField = ""; 

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    //handle escaped quotes 
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField += '"';
                        i++; 
                    } else
                    {
                        inQuotes = !inQuotes; 
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // End of field 
                    result.Add(currentField.Trim());
                    currentField = "";
                }
                else
                {
                    currentField += c; 
                }
            }

            //Add the final field 
            result.Add(currentField.Trim());

            return result.ToArray(); 
        }

        #endregion
    }
}
