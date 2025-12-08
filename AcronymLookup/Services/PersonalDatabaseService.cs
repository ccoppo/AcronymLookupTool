using AcronymLookup.Models; 
using AcronymLookup.Utilities;
using Microsoft.Data.SqlClient; 
using System;
using System.Collections.Generic;
using System.Collections.Generic; 
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace AcronymLookup.Services
{
    public class PersonalDatabaseService
    {
        private readonly string _connectionString;

        #region Constructors 
        public PersonalDatabaseService (string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString), "Connection String cannot be empty"); 
            }

            _connectionString = connectionString;
            Logger.Log("PersonalDatabaseService created"); 
        }

        #endregion

        #region Search Methods 

        /// <summary>
        /// search for term in personal database
        /// </summary>
        /// <param name="searchTerm"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public AbbreviationData? FindPersonalAbbreviation(string searchTerm, int userId)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return null;

            try
            {
                string cleanedTerm = searchTerm.Trim().ToUpper();

                string query = @" SELECT Abbreviation, Definition, Category, Notes
                        FROM PersonalAbbreviations
                        WHERE UserID = @UserID
                            AND UPPER(Abbreviation) = @SearchTerm
                            AND IsActive = 1";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {

                        command.Parameters.AddWithValue("@UserID", userId);
                        command.Parameters.AddWithValue("@SearchTerm", cleanedTerm);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string abbreviation = reader.GetString(0);
                                string definition = reader.GetString(1);
                                string category = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                string notes = reader.IsDBNull(3) ? "" : reader.GetString(3);

                                Logger.Log($"Found in personal database: {abbreviation}");
                                return new AbbreviationData(abbreviation, definition, category, notes, "Personal");
                            }
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error searching personal database: {ex.Message}");
                return null;
            }
        }

        public List<AbbreviationData> GetAllPersonalAbbreviations(int userId)
        {
            List<AbbreviationData> results = new List<AbbreviationData>();

            try
            {
                string query = @"
                    SELECT Abbreviation, Definition, Category, Notes
                    FROM PersonalAbbreviations 
                    WHERE UserID = @UserID
                        AND IsActive = 1
                    ORDER BY Abbreviation";

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
                                string abbreviation = reader.GetString(0);
                                string definition = reader.GetString(1);
                                string category = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                string notes = reader.IsDBNull(3) ? "" : reader.GetString(3);

                                results.Add(new AbbreviationData(abbreviation, definition, category, notes, "Personal"));
                            }
                        }
                    }
                }

                Logger.Log($"Retrieved {results.Count} personal abbreviations for user {userId}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting personal abbreviations: {ex.Message}");

            }
            return results; 

        }

        #endregion


        #region Operation Methods 

        /// <summary>
        /// Add a new term to users personal database
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="abbreviation"></param>
        /// <param name="definition"></param>
        /// <param name="category"></param>
        /// <param name="notes"></param>
        /// <returns></returns>
        public bool AddPersonalAbbreviation(int userId, string abbreviation, string definition, string category = "", string notes = "")
        {
            try
            {
                var existing = FindPersonalAbbreviation(abbreviation, userId); 
                if (existing != null)
                {
                    Logger.Log($"Personal abbreviation '{abbreviation}' already exists for user {userId}");
                    return false; 
                }

                string query = @"
                    INSERT INTO PersonalAbbreviations 
                        (UserID, Abbreviation, Definition, Category, Notes, RequestStatus, IsActive)
                    VALUES 
                        (@UserID, @Abbreviation, @Definition, @Category, @Notes, 'Personal', 1)"; 

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open(); 
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        command.Parameters.AddWithValue("@Abbreviation", abbreviation.Trim().ToUpper());
                        command.Parameters.AddWithValue("@Definition", definition.Trim());
                        command.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(category) ? DBNull.Value : category.Trim());
                        command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes.Trim());

                        command.ExecuteNonQuery(); 
                    }
                }

                Logger.Log($"Added personal abbreviation '{abbreviation}' for user {userId}");
                return true; 
            }
            catch (Exception ex)
            {
                Logger.Log($"Error adding personal abbreviation: {ex.Message}");
                return false; 
            }
        }


        /// <summary>
        /// update term in personal database
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="abbreviation"></param>
        /// <param name="newDefinition"></param>
        /// <param name="newCategory"></param>
        /// <param name="newNotes"></param>
        /// <returns></returns>
        public bool UpdatePersonalAbbreviation(int userId, string abbreviation, string newDefinition, string newCategory = "", string newNotes = "")
        {
            try
            {
                string query = @" 
                    UPDATE PersonalAbbreviations
                    SET 
                        Definition = @Definition, 
                        Category = @Category, 
                        Notes = @Notes, 
                        DateModified = GETDATE()
                    WHERE UserID = @UserID
                        AND UPPER(Abbreviation) = @Abbreviation
                        AND IsActive = 1"; 

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open(); 
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        command.Parameters.AddWithValue("@Abbreviation", abbreviation.Trim().ToUpper());
                        command.Parameters.AddWithValue("@Definition", newDefinition.Trim());
                        command.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(newCategory) ? DBNull.Value : newCategory.Trim());
                        command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(newNotes) ? DBNull.Value : newNotes.Trim());

                        int rowsAffected = command.ExecuteNonQuery(); 

                        if(rowsAffected > 0)
                        {
                            Logger.Log($"Updated personal abbreviation '{abbreviation}' for user {userId}");
                            return true; 
                        }
                    }
                }
                Logger.Log($"No personal abbreviation found to update '{abbreviation}' for user {userId}");
                return false;

            }catch (Exception ex)
            {
                Logger.Log($"Error updating personal abbreviation: {ex.Message}");
                return false; 
            } 
        }

        /// <summary>
        /// delete term in personal database 
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="abbreviation"></param>
        /// <returns></returns>
        public bool DeletePersonalAbbreviation(int userId, string abbreviation)
        {
            try
            {
                string query = @"
                    UPDATE PersonalAbbreviations
                    SET IsActive = 0
                    WHERE UserID = @UserID
                        AND UPPER(Abbreviation) = @Abbreviation
                        AND IsActive = 1"; 

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open(); 
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        command.Parameters.AddWithValue("@Abbreviation", abbreviation);

                        int rowsAffected = command.ExecuteNonQuery(); 

                        if (rowsAffected > 0)
                        {
                            Logger.Log($"Deleted personal abbreviation '{abbreviation}' for user {userId}");
                            return true; 
                        }
                    }

                    Logger.Log($"No personal abbreviation found to delete: {abbreviation}");
                    return false; 
                }
            }catch (Exception ex)
            {
                Logger.Log($"Error deleting personal abbreviation: {ex.Message}");
                return false; 
            }
        }

        #endregion

        #region Request Promotion Methods 

        /// <summary>
        /// send request for term to be added to the project db
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="projectId"></param>
        /// <param name="abbreviation"></param>
        /// <param name="requestReason"></param>
        /// <returns></returns>
        public bool RequestPromotionToProject(int userId, int projectId, string abbreviation, string requestReason = " ")
        {

            try
            {
                var personalTerm = FindPersonalAbbreviation(abbreviation, userId);

                if (personalTerm == null)
                {
                    Logger.Log($"Personal Term '{abbreviation}' not found");
                    return false;
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            string updatePersonalQuery = @"
                                UPDATE PersonalAbbreviations 
                                SET RequestedForProjectID = @ProjectID, 
                                    RequestedStatus = 'Requested' 
                                WHERE UserID = @UserID 
                                    AND UPPER(Abbreviation) = @Abbreviation
                                    AND IsActive = 1";

                            using (SqlCommand command = new SqlCommand(updatePersonalQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@UserID", userId);
                                command.Parameters.AddWithValue("@ProjectID", projectId);
                                command.Parameters.AddWithValue("@Abbreviation", abbreviation.Trim().ToUpper());
                                command.ExecuteNonQuery();
                            }

                            string createRequestQuery = @"
                                INSERT INTO TermRequests
                                    (ProjectID, RequestedByUserID, RequestType, NewAbbreviation, NewDefinition, NewCategory, NewNotes, RequestReason, Status)
                                VALUES 
                                    (@ProjectID, @UserID, 'Add', @Abbreviation, @Definition, @Category, @Notes, @Reason, 'Pending')";

                            using (SqlCommand command = new SqlCommand(createRequestQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@ProjectID", projectId);
                                command.Parameters.AddWithValue("@UserID", userId);
                                command.Parameters.AddWithValue("@Abbreviation", personalTerm.Abbreviation);
                                command.Parameters.AddWithValue("@Definition", personalTerm.Definition);
                                command.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(personalTerm.Category) ? DBNull.Value : personalTerm.Category);
                                command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(personalTerm.Notes) ? DBNull.Value : personalTerm.Notes);
                                command.Parameters.AddWithValue("@Reason", string.IsNullOrWhiteSpace(requestReason) ? "Promotion from personal database" : requestReason);

                                command.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            Logger.Log($"Created promotion request for '{abbreviation}' to project {projectId}");
                            return true;

                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }catch (Exception ex)
            {
                Logger.Log($"Error creating promotion request: {ex.Message}");
                return false; 
            }
           
        }

        #endregion

        #region Helpers 

        /// <summary>
        /// get count of num of abbreviations in the personal database
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public int GetPersonalAbbreviationCount(int userId)
        {
            try
            {
                string query = @"
                    SELECT COUNT(*)
                    FROM PersonalAbbreviations
                    WHERE UserID = @UserID AND IsActive = 1"; 

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        return command.ExecuteNonQuery(); 
                    }
                }
            }catch (Exception ex)
            {
                Logger.Log($"Error, failed to get personal abbreviation count: {ex.Message}");
                return 0; 
            }
        }

        #endregion 


    }
}