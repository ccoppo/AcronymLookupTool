using System;
using Microsoft.Data.SqlClient;
using AcronymLookup.Utilities;

namespace AcronymLookup.Services
{
    /// <summary>
    /// Handles logging changes for admin/security purposes
    /// allows for a complete audit trail for compliance purposes  
    /// </summary>
    public class AuditService
    {
        #region Private Fields
        private readonly string _connectionString;
        #endregion

        #region Constructor
        public AuditService(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString), "Connection string cannot be empty");

            _connectionString = connectionString;
            Logger.Log("AuditService created");
        }
        #endregion

        #region Audit Logging Methods

        /// <summary>
        /// logs a change to the abbreviation
        /// </summary>
        /// <param name="abbreviationId"></param>
        /// <param name="projectId"></param>
        /// <param name="fieldChanged"></param>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        /// <param name="editedByUserId"></param>
        /// <param name="changeReason"></param>
        /// <returns></returns>
        public bool LogChange(
            int abbreviationId,
            int projectId,
            string fieldChanged,
            string oldValue,
            string newValue,
            int editedByUserId,
            string changeReason = "")
        {
            try
            {
                string query = @"
                    INSERT INTO EditHistory 
                        (AbbreviationID, ProjectID, FieldChanged, OldValue, NewValue, EditedByUserID, DateEdited, ChangeReason)
                    VALUES 
                        (@AbbreviationID, @ProjectID, @FieldChanged, @OldValue, @NewValue, @EditedByUserID, GETDATE(), @ChangeReason)";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@AbbreviationID", abbreviationId);
                        command.Parameters.AddWithValue("@ProjectID", projectId);
                        command.Parameters.AddWithValue("@FieldChanged", fieldChanged);
                        command.Parameters.AddWithValue("@OldValue", oldValue ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@NewValue", newValue ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@EditedByUserID", editedByUserId);
                        command.Parameters.AddWithValue("@ChangeReason", 
                            string.IsNullOrWhiteSpace(changeReason) ? (object)DBNull.Value : changeReason);

                        command.ExecuteNonQuery();
                    }
                }

                Logger.Log($"Audit log created: {fieldChanged} changed on AbbreviationID {abbreviationId}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error logging change to EditHistory: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Logs that a term has been added to the database 
        /// </summary>
        /// <param name="abbreviationId"></param>
        /// <param name="projectId"></param>
        /// <param name="abbreviation"></param>
        /// <param name="definition"></param>
        /// <param name="addedByUserId"></param>
        /// <returns></returns>
        public bool LogTermAdded(
            int abbreviationId,
            int projectId,
            string abbreviation,
            string definition,
            int addedByUserId)
        {
            try
            {
                // Log the addition as multiple field changes
                LogChange(abbreviationId, projectId, "Abbreviation", "", abbreviation, addedByUserId, "Term added");
                LogChange(abbreviationId, projectId, "Definition", "", definition, addedByUserId, "Term added");

                Logger.Log($"Audit: Term '{abbreviation}' added to project {projectId}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error logging term addition: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// logs that a term has been deleted from the database 
        /// </summary>
        /// <param name="abbreviationId"></param>
        /// <param name="projectId"></param>
        /// <param name="abbreviation"></param>
        /// <param name="deletedByUserId"></param>
        /// <param name="deleteReason"></param>
        /// <returns></returns>
        public bool LogTermDeleted(
            int abbreviationId,
            int projectId,
            string abbreviation,
            int deletedByUserId,
            string deleteReason = "")
        {
            try
            {
                LogChange(
                    abbreviationId, 
                    projectId, 
                    "IsActive", 
                    "1", 
                    "0", 
                    deletedByUserId, 
                    string.IsNullOrWhiteSpace(deleteReason) ? "Term deleted" : deleteReason);

                Logger.Log($"Audit: Term '{abbreviation}' deleted from project {projectId}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error logging term deletion: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Logs multiple field changes at once (for complex edits)
        /// </summary>
        /// <param name="abbreviationId"></param>
        /// <param name="projectId"></param>
        /// <param name="editedByUserId"></param>
        /// <param name="changeReason"></param>
        /// <param name="changes"></param>
        /// <returns></returns>
        public bool LogMultipleChanges(
            int abbreviationId,
            int projectId,
            int editedByUserId,
            string changeReason,
            params (string fieldName, string oldValue, string newValue)[] changes)
        {
            try
            {
                foreach (var change in changes)
                {
                    if (change.oldValue != change.newValue)
                    {
                        LogChange(
                            abbreviationId,
                            projectId,
                            change.fieldName,
                            change.oldValue,
                            change.newValue,
                            editedByUserId,
                            changeReason);
                    }
                }

                Logger.Log($"Audit: Multiple changes logged for AbbreviationID {abbreviationId}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error logging multiple changes: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// allows you to get the change history for a specific term
        /// </summary>
        /// <param name="abbreviationId"></param>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public string GetChangeHistory(int abbreviationId, int projectId)
        {
            try
            {
                string query = @"
                    SELECT TOP 10
                        eh.FieldChanged,
                        eh.OldValue,
                        eh.NewValue,
                        eh.DateEdited,
                        u.FullName,
                        eh.ChangeReason
                    FROM EditHistory eh
                    INNER JOIN Users u ON eh.EditedByUserID = u.UserID
                    WHERE eh.AbbreviationID = @AbbreviationID 
                        AND eh.ProjectID = @ProjectID
                    ORDER BY eh.DateEdited DESC";

                string history = "";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@AbbreviationID", abbreviationId);
                        command.Parameters.AddWithValue("@ProjectID", projectId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string field = reader.GetString(0);
                                string oldValue = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                string newValue = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                DateTime date = reader.GetDateTime(3);
                                string editor = reader.GetString(4);
                                string reason = reader.IsDBNull(5) ? "" : reader.GetString(5);

                                history += $"{date:yyyy-MM-dd HH:mm} - {editor} changed {field}\n";
                                if (!string.IsNullOrWhiteSpace(reason))
                                    history += $"  Reason: {reason}\n";
                            }
                        }
                    }
                }

                return string.IsNullOrWhiteSpace(history) ? "No change history found" : history;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting change history: {ex.Message}");
                return "Error retrieving change history";
            }
        }

        #endregion
    }
}