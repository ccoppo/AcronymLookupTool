using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcronymLookup.Utilities;
using Microsoft.Data.SqlClient;

namespace AcronymLookup.Services
{

    public class PermissionService
    {
        private readonly string _connectionString; 

        public PermissionService(string connectionString)
        {
            if(string.IsNullOrWhiteSpace(connectionString)) 
                throw new ArgumentNullException(nameof(connectionString), "Connection String Cannot be NULL or empty");

            _connectionString = connectionString;
            Logger.Log("PermissionService created"); 

        }

        #region PermissionCheckingMethods

        public bool IsSystemAdmin(int userID)
        {
            try
            {
                string query = @" 
                    SELECT IsSystemAdmin
                    FROM Users
                    WHERE UserId = @UserID AND IsActive = 1";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open(); 

                    using(SqlCommand command = new SqlCommand(_connectionString))
                    {
                        command.Parameters.AddWithValue("@UserID", userID);
                        object result = command.ExecuteScalar(); 

                        if (result != null && result != DBNull.Value)
                        {
                            return (bool)result; 
                        }
                    }
                }
                return false; 
            }catch (Exception ex)
            {
                Logger.Log($"Error checking system admin statues: {ex.Message}");
                return false; 
            }
        }

        public UserProjectPermissions GetUserPermissions(int userId, int projectId)
        {
            try
            {
                if (IsSystemAdmin(userId))
                {
                    Logger.Log($"User {userId} is System Admin - granting all permissions");
                    return UserProjectPermissions.CreateSystemAdminPermissions();
                }

                string query = @" 
                    SELECT 
                        Role, 
                        CanViewTerms, 
                        CanAddTermsDirectly, 
                        CanEditTermsDirectly, 
                        CanEditTermsDirectly, 
                        CanDeleteTermsDirectly, 
                        CanRequestAdditions, 
                        CanRequestEdits,
                        CanRequestDeletions, 
                        CanApproveRequests, 
                        CanManageMembers, 
                        CanAssignRoles
                    FROM ProjectMembers 
                    WHERE UserID = @UserID
                        AND ProjectID = @ProjectID
                        AND IsActive = 1";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open(); 
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", userId);
                        command.Parameters.AddWithValue("@ProjectID", projectId); 

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new UserProjectPermissions
                                {
                                    Role = reader.IsDBNull(0) ? "None" : reader.GetString(0),
                                    CanViewTerms = reader.GetBoolean(1),
                                    CanAddTermsDirectly = reader.GetBoolean(2),
                                    CanEditTermsDirectly = reader.GetBoolean(3),
                                    CanDeleteTermsDirectly = reader.GetBoolean(4),
                                    CanRequestAdditions = reader.GetBoolean(5),
                                    CanRequestEdits = reader.GetBoolean(6),
                                    CanRequestDeletions = reader.GetBoolean(7),
                                    CanApproveRequests = reader.GetBoolean(8),
                                    CanManageMembers = reader.GetBoolean(9),
                                    CanAssignRoles = reader.GetBoolean(10),
                                }; 
                            }
                        }
                    }
                }

                Logger.Log($"User {userId} not found in project {projectId}");
                return UserProjectPermissions.CreateNoPermissions(); 
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting user permissions: {ex.Message}");
                return UserProjectPermissions.CreateNoPermissions(); 
            }
        }

        public bool CanViewTerms(int userId, int projectId)
        {
            var permissions = GetUserPermissions(userId, projectId);
            return permissions.CanViewTerms; 
        }

        public bool CanAddTermsDirectly (int userId, int projectId)
        {
            var permissions = GetUserPermissions(userId, projectId);
            return permissions.CanAddTermsDirectly; 
        }

        public bool CanEditTermsDirectly(int userId, int projectId)
        {
            var permissions = GetUserPermissions(userId, projectId);
            return permissions.CanEditTermsDirectly; 
        }

        public bool CanDeleteTermsDirectly(int userId, int projectId)
        {
            var permissions = GetUserPermissions(userId, projectId);
            return permissions.CanDeleteTermsDirectly; 
        }

        public bool CanApproveRequests (int userId, int projectId)
        {
            var permissions = GetUserPermissions(userId, projectId);
            return permissions.CanApproveRequests; 
        }

        public string GetPermissionSummary(int userId, int projectId)
        {
            var permissions = GetUserPermissions(userId, projectId);

            if (permissions.Role == "SystemAdmin")
            {
                return "System Administrator (Full Access)";
            }

            if (permissions.Role == "None")
            {
                return "No Access";
            }

            return $"{permissions.Role} - " +
                $"View: {(permissions.CanViewTerms ? "yes" : "no")}" +
                $"Add: {(permissions.CanAddTermsDirectly ? "yes" : "no")}" +
                $"Edit: {(permissions.CanEditTermsDirectly ? "yes" : "no")}" +
                $"Delete: {(permissions.CanDeleteTermsDirectly ? "yes" : "no")}"; 

        }

        #endregion


    }

    #region PermissionDataClass 

    public class UserProjectPermissions
    {
        public string Role { get; set; } = "None"; 
        public bool CanViewTerms { get; set; } 
        public bool CanAddTermsDirectly { get; set; } 
        public bool CanEditTermsDirectly { get; set; } 
        public bool CanDeleteTermsDirectly { get; set; }
        public bool CanRequestAdditions { get; set; } 
        public bool CanRequestEdits { get; set; } 
        public bool CanRequestDeletions { get; set; } 
        public bool CanApproveRequests { get; set; } 
        public bool CanManageMembers { get; set; }
        public bool CanAssignRoles { get; set; } 

        public static UserProjectPermissions CreateSystemAdminPermissions()
        {
            return new UserProjectPermissions
            {
                Role = "SystemAdmin",
                CanViewTerms = true,
                CanAddTermsDirectly = true,
                CanEditTermsDirectly = true,
                CanDeleteTermsDirectly = true,
                CanRequestAdditions = true,
                CanRequestEdits = true,
                CanRequestDeletions = true,
                CanApproveRequests = true,
                CanManageMembers = true,
                CanAssignRoles = true,
            }; 
        }

        public static UserProjectPermissions CreateNoPermissions()
        {
            return new UserProjectPermissions
            {
                Role = "None",
                CanViewTerms = false,
                CanAddTermsDirectly = false,
                CanEditTermsDirectly = false,
                CanDeleteTermsDirectly = false,
                CanRequestAdditions = false,
                CanRequestEdits = false,
                CanRequestDeletions = false,
                CanApproveRequests = false,
                CanManageMembers = false,
                CanAssignRoles = false,
            }; 
        }
    }

    #endregion 
}


