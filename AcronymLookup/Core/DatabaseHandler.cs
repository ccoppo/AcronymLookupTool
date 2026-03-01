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

        /// <summary>
        /// Gets all projects that a user has access to
        /// Returns list of project info including ID, name, and code
        /// </summary>
        public List<UserProjectInfo> GetUserProjects(int userId)
        {
            var projects = new List<UserProjectInfo>();
            
            try
            {
                string query = @"
                    SELECT 
                        p.ProjectID,
                        p.ProjectName,
                        p.ProjectCode,
                        p.Description,
                        pm.Role
                    FROM ProjectMembers pm
                    INNER JOIN Projects p ON pm.ProjectID = p.ProjectID
                    WHERE pm.UserID = @UserID
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
                        
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                projects.Add(new UserProjectInfo
                                {
                                    ProjectID = reader.GetInt32(0),
                                    ProjectName = reader.GetString(1),
                                    ProjectCode = reader.GetString(2),
                                    Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    UserRole = reader.IsDBNull(4) ? "Viewer" : reader.GetString(4)
                                });
                            }
                        }
                    }
                }
                
                Logger.Log($"Found {projects.Count} projects for user {userId}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting user projects: {ex.Message}");
            }
            
            return projects;
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

        #region Approval Queue Methods

        /// <summary>
        /// Returns all Pending TermRequests for the given project.
        /// Joins Users table so the reviewer sees a real display name.
        /// </summary>
        public List<TermRequest> GetPendingRequests(int projectId)
        {
            var results = new List<TermRequest>();

            try
            {
                string query = @"
                    SELECT
                        tr.TermRequestID,
                        tr.ProjectID,
                        tr.RequestedByUserID,
                        u.FullName             AS RequestedByUserName,
                        tr.RequestType,
                        tr.NewAbbreviation,
                        tr.NewDefinition,
                        tr.NewCategory,
                        tr.NewNotes,
                        tr.AbbreviationID,
                        tr.EditedDefinition,
                        tr.EditedCategory,
                        tr.EditedNotes,
                        tr.RequestReason,
                        tr.DateRequested,
                        tr.Status,
                        -- For Edit/Delete requests, pull the existing abbreviation text
                        a.Abbreviation         AS ExistingAbbreviation
                    FROM TermRequests tr
                    INNER JOIN Users u ON tr.RequestedByUserID = u.UserID
                    LEFT JOIN Abbreviations a ON tr.AbbreviationID = a.AbbreviationID
                    WHERE tr.ProjectID = @ProjectID
                    AND tr.Status = 'Pending'
                    ORDER BY tr.DateRequested ASC";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProjectID", projectId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string requestType = reader.GetString(4);

                                // For Edit/Delete, use the existing abbreviation text from the JOIN.
                                // For Add, use NewAbbreviation directly.
                                string displayAbbreviation = requestType == "Add"
                                    ? (reader.IsDBNull(5)  ? string.Empty : reader.GetString(5))
                                    : (reader.IsDBNull(16) ? string.Empty : reader.GetString(16));

                                var request = new TermRequest
                                {
                                    TermRequestID        = reader.GetInt32(0),
                                    ProjectID            = reader.GetInt32(1),
                                    RequestedByUserID    = reader.GetInt32(2),
                                    RequestedByUserName  = reader.IsDBNull(3) ? "Unknown" : reader.GetString(3),
                                    RequestType          = requestType,
                                    NewAbbreviation      = displayAbbreviation,   // normalised above
                                    NewDefinition        = reader.IsDBNull(6)  ? null : reader.GetString(6),
                                    NewCategory          = reader.IsDBNull(7)  ? null : reader.GetString(7),
                                    NewNotes             = reader.IsDBNull(8)  ? null : reader.GetString(8),
                                    AbbreviationID       = reader.IsDBNull(9)  ? null : reader.GetInt32(9),
                                    EditedDefinition     = reader.IsDBNull(10) ? null : reader.GetString(10),
                                    EditedCategory       = reader.IsDBNull(11) ? null : reader.GetString(11),
                                    EditedNotes          = reader.IsDBNull(12) ? null : reader.GetString(12),
                                    RequestReason        = reader.IsDBNull(13) ? null : reader.GetString(13),
                                    DateRequested        = reader.GetDateTime(14),
                                    Status               = reader.GetString(15),
                                };

                                results.Add(request);
                            }
                        }
                    }
                }

                Logger.Log($"Retrieved {results.Count} pending request(s) for project {projectId}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting pending requests: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Approves a pending TermRequest.
        /// For Add:    inserts the term into Abbreviations + AbbreviationProjects.
        /// For Edit:   applies the proposed changes to the existing Abbreviations row.
        /// For Delete: soft-deletes the existing Abbreviations row.
        /// All three then mark the request Approved in TermRequests.
        /// Everything runs in a single transaction.
        /// </summary>
        public bool ApproveRequest(int termRequestId, int reviewerUserId, string? reviewComment = null)
        {
            try
            {
                // Load the full request first (outside the transaction — read-only)
                TermRequest? request = GetRequestById(termRequestId);
                if (request == null)
                {
                    Logger.Log($"ApproveRequest: TermRequestID {termRequestId} not found");
                    return false;
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // ── 1. Apply the requested change ────────────────────────────

                            if (request.RequestType == "Add")
                            {
                                // Insert new term into Abbreviations
                                string insertAbbrev = @"
                                    INSERT INTO Abbreviations
                                        (Abbreviation, Definition, Category, Notes,
                                        CreatedBy, CreatedByUserID, IsActive)
                                    VALUES
                                        (@Abbreviation, @Definition, @Category, @Notes,
                                        'Approved Request', @CreatedByUserID, 1);
                                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                                int newAbbrevId;
                                using (SqlCommand cmd = new SqlCommand(insertAbbrev, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@Abbreviation",     request.NewAbbreviation!.Trim().ToUpper());
                                    cmd.Parameters.AddWithValue("@Definition",       request.NewDefinition ?? string.Empty);
                                    cmd.Parameters.AddWithValue("@Category",         string.IsNullOrWhiteSpace(request.NewCategory) ? DBNull.Value : request.NewCategory.Trim());
                                    cmd.Parameters.AddWithValue("@Notes",            string.IsNullOrWhiteSpace(request.NewNotes)    ? DBNull.Value : request.NewNotes.Trim());
                                    cmd.Parameters.AddWithValue("@CreatedByUserID",  request.RequestedByUserID);
                                    newAbbrevId = (int)cmd.ExecuteScalar();
                                }

                                // Link to project
                                string linkProject = @"
                                    INSERT INTO AbbreviationProjects
                                        (AbbreviationID, ProjectID, AddedByUserID, IsActive)
                                    VALUES
                                        (@AbbreviationID, @ProjectID, @AddedByUserID, 1)";

                                using (SqlCommand cmd = new SqlCommand(linkProject, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@AbbreviationID",  newAbbrevId);
                                    cmd.Parameters.AddWithValue("@ProjectID",        request.ProjectID);
                                    cmd.Parameters.AddWithValue("@AddedByUserID",    reviewerUserId);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            else if (request.RequestType == "Edit" && request.AbbreviationID.HasValue)
                            {
                                string updateAbbrev = @"
                                    UPDATE Abbreviations
                                    SET Definition     = @Definition,
                                        Category       = @Category,
                                        Notes          = @Notes,
                                        ModifiedBy     = 'Approved Request',
                                        ModifiedByUserID = @ModifiedByUserID,
                                        DateModified   = GETDATE()
                                    WHERE AbbreviationID = @AbbreviationID
                                    AND IsActive = 1";

                                using (SqlCommand cmd = new SqlCommand(updateAbbrev, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@Definition",       request.EditedDefinition ?? string.Empty);
                                    cmd.Parameters.AddWithValue("@Category",         string.IsNullOrWhiteSpace(request.EditedCategory) ? DBNull.Value : request.EditedCategory.Trim());
                                    cmd.Parameters.AddWithValue("@Notes",            string.IsNullOrWhiteSpace(request.EditedNotes)    ? DBNull.Value : request.EditedNotes.Trim());
                                    cmd.Parameters.AddWithValue("@ModifiedByUserID", reviewerUserId);
                                    cmd.Parameters.AddWithValue("@AbbreviationID",   request.AbbreviationID.Value);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            else if (request.RequestType == "Delete" && request.AbbreviationID.HasValue)
                            {
                                string softDelete = @"
                                    UPDATE Abbreviations
                                    SET IsActive         = 0,
                                        ModifiedBy       = 'Approved Deletion',
                                        ModifiedByUserID = @ModifiedByUserID,
                                        DateModified     = GETDATE()
                                    WHERE AbbreviationID = @AbbreviationID
                                    AND IsActive = 1";

                                using (SqlCommand cmd = new SqlCommand(softDelete, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ModifiedByUserID", reviewerUserId);
                                    cmd.Parameters.AddWithValue("@AbbreviationID",   request.AbbreviationID.Value);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // ── 2. Mark request as Approved ──────────────────────────────
                            MarkRequestReviewed(termRequestId, "Approved", reviewerUserId, reviewComment, connection, transaction);

                            transaction.Commit();
                            Logger.Log($"Approved TermRequestID {termRequestId} (type: {request.RequestType})");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Logger.Log($"ApproveRequest transaction rolled back: {ex.Message}");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error approving request: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Rejects a pending TermRequest — only updates the TermRequests row.
        /// No changes are made to Abbreviations.
        /// </summary>
        public bool RejectRequest(int termRequestId, int reviewerUserId, string? reviewComment = null)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            MarkRequestReviewed(termRequestId, "Rejected", reviewerUserId, reviewComment, connection, transaction);
                            transaction.Commit();
                            Logger.Log($"Rejected TermRequestID {termRequestId}");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Logger.Log($"RejectRequest transaction rolled back: {ex.Message}");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error rejecting request: {ex.Message}");
                return false;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Loads a single TermRequest by ID (used internally before transactions).
        /// </summary>
        private TermRequest? GetRequestById(int termRequestId)
        {
            try
            {
                string query = @"
                    SELECT
                        tr.TermRequestID, tr.ProjectID, tr.RequestedByUserID,
                        tr.RequestType,
                        tr.NewAbbreviation, tr.NewDefinition, tr.NewCategory, tr.NewNotes,
                        tr.AbbreviationID,
                        tr.EditedDefinition, tr.EditedCategory, tr.EditedNotes,
                        tr.RequestReason, tr.DateRequested, tr.Status
                    FROM TermRequests tr
                    WHERE tr.TermRequestID = @TermRequestID";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@TermRequestID", termRequestId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new TermRequest
                                {
                                    TermRequestID     = reader.GetInt32(0),
                                    ProjectID         = reader.GetInt32(1),
                                    RequestedByUserID = reader.GetInt32(2),
                                    RequestType       = reader.GetString(3),
                                    NewAbbreviation   = reader.IsDBNull(4)  ? null : reader.GetString(4),
                                    NewDefinition     = reader.IsDBNull(5)  ? null : reader.GetString(5),
                                    NewCategory       = reader.IsDBNull(6)  ? null : reader.GetString(6),
                                    NewNotes          = reader.IsDBNull(7)  ? null : reader.GetString(7),
                                    AbbreviationID    = reader.IsDBNull(8)  ? null : reader.GetInt32(8),
                                    EditedDefinition  = reader.IsDBNull(9)  ? null : reader.GetString(9),
                                    EditedCategory    = reader.IsDBNull(10) ? null : reader.GetString(10),
                                    EditedNotes       = reader.IsDBNull(11) ? null : reader.GetString(11),
                                    RequestReason     = reader.IsDBNull(12) ? null : reader.GetString(12),
                                    DateRequested     = reader.GetDateTime(13),
                                    Status            = reader.GetString(14),
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error loading request by ID: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Shared helper: stamps the review outcome onto a TermRequests row.
        /// Must be called inside an open transaction.
        /// </summary>
        private static void MarkRequestReviewed(
            int termRequestId,
            string status,
            int reviewerUserId,
            string? reviewComment,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            string update = @"
                UPDATE TermRequests
                SET Status           = @Status,
                    ReviewedByUserID = @ReviewedByUserID,
                    ReviewComment    = @ReviewComment,
                    DateReviewed     = GETDATE()
                WHERE TermRequestID = @TermRequestID";

            using (SqlCommand cmd = new SqlCommand(update, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@Status",           status);
                cmd.Parameters.AddWithValue("@ReviewedByUserID", reviewerUserId);
                cmd.Parameters.AddWithValue("@ReviewComment",    string.IsNullOrWhiteSpace(reviewComment) ? DBNull.Value : reviewComment!.Trim());
                cmd.Parameters.AddWithValue("@TermRequestID",    termRequestId);
                cmd.ExecuteNonQuery();
            }
        }

        #endregion
    }
}