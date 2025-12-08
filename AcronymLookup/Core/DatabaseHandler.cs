using AcronymLookup.Models;
using AcronymLookup.Utilities;
using AcronymLookup.Services; 
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AcronymLookup.Core
{
    public class DatabaseHandler
    {
        #region Private Fields 
        private readonly string _connectionString;
        private int _currentUserId;
        private int _currentProjectId; 
        private readonly AuditService _auditService;


        #endregion

        #region Constructor 

        public DatabaseHandler(string connectionString, AuditService auditService)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException("Connection string cannot be empty", nameof(connectionString)); 

            _connectionString = connectionString;

            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));

            Logger.Log("DatabaseHandler created"); 

        }

        #endregion

        #region public user methods 
        public void SetUserContext(int userId, int projectId)
        {
            _currentUserId = userId;
            _currentProjectId = projectId;
            Logger.Log($"Database context set: UserID={userId}, ProjectID={projectId}");
        }

        public int CurrentUserId => _currentUserId;
        public int CurrentProjectId => _currentProjectId; 

        public int? GetUserIdByWindowsUsername(string windowsUsername)
        {
            try
            {
                string query = @"SELECT UserID FROM Users WHERE WindowsUsername = @WindowsUsername AND isActive = 1"; 

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open(); 

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@WindowsUsername", windowsUsername);

                        object result = command.ExecuteScalar(); 
                        if (result != null && result != DBNull.Value)
                        {
                            return (int)result; 
                        }
                    }
                }

                Logger.Log($"User not found: {windowsUsername}");
                return null;
            }catch (Exception ex)
            {
                Logger.Log($"error getting user ID: {ex.Message}");
                return null; 
            }
        }

        public int? GetUserFirstProject(int userId)
        {
            try
            {
                string query = @"
                    SELECT TOP 1 pm.ProjectID
                    FROM ProjectMembers pm
                    INNER JOIN Projects p on pm.ProjectID = p.ProjectID
                    WHERE pm.USERID = @UserID
                    AND pm.IsActive = 1
                    AND p.IsActive = 1
                    AND pm.CanViewTerms = 1
                    ORDER BY pm.DateAdded ASC";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        object result = command.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            return (int)result;
                        }
                    }
                }

                Logger.Log($"no projects found for user ID: {userId}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting user project: {ex.Message}");
                return null;
            }
        }
        #endregion 


        #region Public Methods 
        /// <summary>
        /// Test connection to database
        /// </summary>
        /// <returns></returns>
        public bool TestConnection()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    Logger.Log("Database connection test: SUCCESS");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Database connection test FAILED: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// pull abbreviation from database
        /// </summary>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        public AbbreviationData? FindAbbreviation(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return null; 

            try
            {
                string cleanedTerm = searchTerm.Trim().ToUpper();

                //SQL query to find matching abbreviations
                string query = @"
                    SELECT DISTINCT
                        a.Abbreviation, 
                        a.Definition, 
                        a.Category, 
                        a.Notes
                    FROM Abbreviations a
                    INNER JOIN AbbreviationProjects ap ON a.AbbreviationID = ap.AbbreviationID
                    INNER JOIN ProjectMembers pm ON ap.ProjectID = pm.ProjectID
                    WHERE UPPER(a.Abbreviation) = @SearchTerm
                        AND ap.ProjectID = @ProjectID
                        AND pm.UserID = @UserID 
                        AND pm.CanViewTerms = 1
                        AND a.IsActive = 1 
                        AND ap.IsActive = 1
                        AND pm.IsActive = 1";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        //add parameter to prevent SQL injection 
                        command.Parameters.AddWithValue("@SearchTerm", cleanedTerm);
                        command.Parameters.AddWithValue("@ProjectID", _currentProjectId);
                        command.Parameters.AddWithValue("@UserID", _currentUserId); 

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                //Create abbreviation data from database row 
                                string abbreviation = reader.GetString(0);
                                string definition = reader.GetString(1);
                                string category = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                string notes = reader.IsDBNull(3) ? "" : reader.GetString(3);

                                Logger.Log($"Found in database: {abbreviation}");
                                return new AbbreviationData(abbreviation, definition, category, notes, "Project"); 
                            }
                        }
                    }
                }

                Logger.Log($"Not found in database: {searchTerm}");
                return null; 
            }catch (Exception ex)
            {
                Logger.Log($"Database search error: {ex.Message}");
                return null; 
            }
        }

        //
        public List<AbbreviationData> GetAllAbbreviations()
        {
            List<AbbreviationData> results = new List<AbbreviationData>(); 

            try
            {
                string query = @"
                    SELECT 
                        Abbreviation, 
                        Definition, 
                        Category, 
                        Notes 
                    FROM Abbreviations 
                    WHERE IsActive = 1 
                    ORDER BY Abbreviation"; 

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string abbreviation = reader.GetString(0);
                            string definition = reader.GetString(1);
                            string category = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            string notes = reader.IsDBNull(3) ? "" : reader.GetString(3);

                            results.Add(new AbbreviationData(abbreviation, definition, category, notes, "Project")); 
                        }
                    }
                }
                Logger.Log($"Retrieved {results.Count} abbreviations from database"); 
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting all abbreviations: {ex.Message}"); 
            }

            return results; 
        }

        public int Count
        {
            get
            {
                try
                {
                    string query = "SELECT COUNT(*) FROM Abbreviations WHERE IsActive = 1";

                    using (SqlConnection connection = new SqlConnection(_connectionString))
                    {
                        connection.Open();

                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            return (int)command.ExecuteScalar();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error getting count: {ex.Message}");
                    return 0;
                }
            }
        }


        public bool AddAbbreviation(string abbreviation, string definition, string category = "", string notes = "", string createdBy = "User")
        {
            try
            {
                //validate input 
                if (string.IsNullOrWhiteSpace(abbreviation))
                {
                    Logger.Log("Cannot add abbreviation: abbreviation is empty");
                    return false; 
                }
                if (string.IsNullOrWhiteSpace(definition))
                {
                    Logger.Log("Cannot add abbreviation: definition is empty");
                    return false; 
                }

                var existing = FindAbbreviation(abbreviation); 
                if (existing != null)
                {
                    Logger.Log($"Abbreviation '{abbreviation}' already exists in database");
                    return false; 
                }

                //sql query to insert new abbreviation 
                string query = @"
                    INSERT INTO Abbreviations 
                        (Abbreviation, Definition, Category, Notes, CreatedBy, DateAdded, DateModified, IsActive)
                    VALUES
                        (@Abbreviation, @Definition, @Category, @Notes, @CreatedBy, GETDATE(), GETDATE(), 1)"; 

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open(); 

                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            //insert abbreviation
                            string insertAbbrevQuery = @" 
                                INSERT INTO Abbreviations (Abbreviation, Definition, Category, Notes, CreatedBy, CreatedByUserID, IsActive)
                                VALUES (@Abbreviation, @Definition, @Category, @Notes, @CreatedBy, @CreatedByUserID, 1); 
                                SELECT CAST(SCOPE_IDENTITY() as int);";

                            int newAbbrevId;
                            using (SqlCommand command = new SqlCommand(insertAbbrevQuery, connection, transaction))
                            {
                                //Add Parameters to prevent sql injection 
                                command.Parameters.AddWithValue("@Abbreviation", abbreviation.Trim().ToUpper());
                                command.Parameters.AddWithValue("@Definition", definition.Trim());
                                command.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(category) ? DBNull.Value : category.Trim());
                                command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes.Trim());
                                command.Parameters.AddWithValue("@CreatedBy", createdBy);
                                command.Parameters.AddWithValue("@CreatedByUserID", _currentUserId);

                                newAbbrevId = (int)command.ExecuteScalar(); 
                            }

                            string linkProjectQuery = @" 
                                INSERT INTO AbbreviationProjects (AbbreviationID, ProjectID, AddedByUserID, IsActive)
                                VALUES (@AbbreviationID, @ProjectID, @AddedByUserID, 1)";

                            using (SqlCommand command = new SqlCommand(linkProjectQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@AbbreviationID", newAbbrevId);
                                command.Parameters.AddWithValue("@ProjectID", _currentProjectId);
                                command.Parameters.AddWithValue("@AddedByUserID", _currentUserId);

                                command.ExecuteNonQuery(); 
                            }

                            transaction.Commit();
                            Logger.Log($"Successfully added abbreviation: {abbreviation}");
                            return true; 
                        }catch (Exception ex)
                        {
                            transaction.Rollback();
                            Logger.Log($"Error in transaction: {ex.Message}");
                            return false; 
                        }
                    }
                }
                    
            }
            catch (Exception ex)
            {
                Logger.Log($"Error adding abbreviation: {ex.Message}");
                return false; 
            }
        }

        /// <summary>
        /// updates an existing abbreviation
        /// </summary>
        /// <param name="abbreviation"></param>
        /// <param name="newDefinition"></param>
        /// <param name="newCategory"></param>
        /// <param name="newNotes"></param>
        /// <param name="editedByUserId"></param>
        /// <param name="changeReason"></param>
        /// <returns></returns>
        public bool UpdateAbbreviation(
            string abbreviation,
            string newDefinition,
            string newCategory,
            string newNotes,
            int editedByUserId,
            string changeReason = "")
        {
            try
            {
                // Get the current values and AbbreviationID
                var current = FindAbbreviation(abbreviation);
                if (current == null)
                {
                    Logger.Log($"Cannot update: '{abbreviation}' not found");
                    return false;
                }

                int abbreviationId = GetAbbreviationId(abbreviation);
                if (abbreviationId == 0)
                {
                    Logger.Log($"Cannot get AbbreviationID for '{abbreviation}'");
                    return false;
                }

                // Build list of changes for audit log
                var changes = new List<(string, string, string)>();
                
                if (current.Definition != newDefinition)
                    changes.Add(("Definition", current.Definition, newDefinition));
                
                if (current.Category != newCategory)
                    changes.Add(("Category", current.Category ?? "", newCategory ?? ""));
                
                if (current.Notes != newNotes)
                    changes.Add(("Notes", current.Notes ?? "", newNotes ?? ""));

                // If nothing changed, no need to update
                if (changes.Count == 0)
                {
                    Logger.Log($"No changes detected for '{abbreviation}'");
                    return true;
                }

                // Update the database
                string query = @"
                    UPDATE Abbreviations
                    SET 
                        Definition = @Definition,
                        Category = @Category,
                        Notes = @Notes,
                        ModifiedBy = @ModifiedBy,
                        ModifiedByUserID = @ModifiedByUserID,
                        DateModified = GETDATE()
                    WHERE UPPER(Abbreviation) = @Abbreviation
                        AND IsActive = 1";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Abbreviation", abbreviation.Trim().ToUpper());
                        command.Parameters.AddWithValue("@Definition", newDefinition.Trim());
                        command.Parameters.AddWithValue("@Category", 
                            string.IsNullOrWhiteSpace(newCategory) ? DBNull.Value : newCategory.Trim());
                        command.Parameters.AddWithValue("@Notes", 
                            string.IsNullOrWhiteSpace(newNotes) ? DBNull.Value : newNotes.Trim());
                        command.Parameters.AddWithValue("@ModifiedBy", "User"); // Could get username
                        command.Parameters.AddWithValue("@ModifiedByUserID", editedByUserId);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Log all changes to audit trail
                            _auditService.LogMultipleChanges(
                                abbreviationId,
                                _currentProjectId,
                                editedByUserId,
                                string.IsNullOrWhiteSpace(changeReason) ? "Term updated" : changeReason,
                                changes.ToArray());

                            Logger.Log($"Updated abbreviation '{abbreviation}'");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error updating abbreviation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// deletes an abbreviation
        /// </summary>
        /// <param name="abbreviation"></param>
        /// <param name="deletedByUserId"></param>
        /// <param name="deleteReason"></param>
        /// <returns></returns>
        public bool DeleteAbbreviation(
            string abbreviation,
            int deletedByUserId,
            string deleteReason = "")
        {
            try
            {
                // Get AbbreviationID before deleting
                int abbreviationId = GetAbbreviationId(abbreviation);
                if (abbreviationId == 0)
                {
                    Logger.Log($"Cannot delete: '{abbreviation}' not found");
                    return false;
                }

                // Soft delete (set IsActive = 0)
                string query = @"
                    UPDATE Abbreviations
                    SET IsActive = 0,
                        ModifiedByUserID = @ModifiedByUserID,
                        DateModified = GETDATE()
                    WHERE UPPER(Abbreviation) = @Abbreviation
                        AND IsActive = 1";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Abbreviation", abbreviation.Trim().ToUpper());
                        command.Parameters.AddWithValue("@ModifiedByUserID", deletedByUserId);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Log deletion to audit trail
                            _auditService.LogTermDeleted(
                                abbreviationId,
                                _currentProjectId,
                                abbreviation,
                                deletedByUserId,
                                deleteReason);

                            Logger.Log($"Deleted abbreviation '{abbreviation}'");
                            return true;
                        }
                    }
                }

                Logger.Log($"Failed to delete '{abbreviation}' - not found or already deleted");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error deleting abbreviation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// gets abbreviation id for audit trail 
        /// </summary>
        /// <param name="abbreviation"></param>
        /// <returns></returns>
        private int GetAbbreviationId(string abbreviation)
        {
            try
            {
                string query = @"
                    SELECT AbbreviationID 
                    FROM Abbreviations 
                    WHERE UPPER(Abbreviation) = @Abbreviation 
                        AND IsActive = 1";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Abbreviation", abbreviation.Trim().ToUpper());

                        object result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return (int)result;
                        }
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting AbbreviationID: {ex.Message}");
                return 0;
            }
        }

        #endregion

   

        #region Private Helper Methods 
        private bool ValidateInput(string abbreviation, string definition)
        {
            // validate input
            if (string.IsNullOrWhiteSpace(abbreviation))
            {
                Logger.Log("Cannot add abbreviation: abbreviation is empty");
                return false;
            }
            if (string.IsNullOrWhiteSpace(definition))
            {
                Logger.Log("Cannot add abbreviation: definition is empty");
                return false;
            }

            var existing = FindAbbreviation(abbreviation);
            if (existing != null)
            {
                Logger.Log($"Abbreviation '{abbreviation}' already exists in database");
                return false;
            }

            return true; 
        }

        #endregion
    }
}