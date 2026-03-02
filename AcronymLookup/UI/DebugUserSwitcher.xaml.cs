#if DEBUG
using AcronymLookup.Utilities;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace AcronymLookup.UI
{
    public partial class DebugUserSwitcher : Window
    {
        // The UserID the caller should use; null means "cancelled, use real auth"
        public int? SelectedUserId { get; private set; } = null;

        private readonly string _connectionString;

        public DebugUserSwitcher(string connectionString)
        {
            InitializeComponent();
            _connectionString = connectionString;
            LoadUsers();
        }

        // Simple display model for the list
        private class UserDisplayItem
        {
            public int UserId { get; set; }
            public string DisplayText { get; set; } = "";
        }

        private void LoadUsers()
        {
            try
            {
                var users = new List<UserDisplayItem>();

                string query = @"
                    SELECT UserID, FullName, Username, IsSystemAdmin
                    FROM Users
                    WHERE IsActive = 1
                    ORDER BY UserID ASC";

                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int userId = reader.GetInt32(0);
                            string fullName = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
                            string username = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2);
                            bool isSysAdmin = !reader.IsDBNull(3) && reader.GetBoolean(3);

                            string sysAdminTag = isSysAdmin ? " [SysAdmin]" : "";

                            users.Add(new UserDisplayItem
                            {
                                UserId = userId,
                                DisplayText = $"[ID {userId}] {fullName} ({username}){sysAdminTag}"
                            });
                        }
                    }
                }

                UserListBox.ItemsSource = users;
                StatusText.Text = $"{users.Count} users loaded.";
                Logger.Log($"[DEBUG] DebugUserSwitcher loaded {users.Count} users");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading users: {ex.Message}";
                Logger.Log($"[DEBUG] DebugUserSwitcher error: {ex.Message}");
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (UserListBox.SelectedItem is UserDisplayItem selected)
            {
                SelectedUserId = selected.UserId;
                Logger.Log($"[DEBUG] Impersonating UserID {selected.UserId}");
                DialogResult = true;
            }
            else
            {
                StatusText.Text = "Please select a user first.";
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedUserId = null;
            DialogResult = false;
        }

        private void UserListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OK_Click(sender, e);
        }
    }
}
#endif