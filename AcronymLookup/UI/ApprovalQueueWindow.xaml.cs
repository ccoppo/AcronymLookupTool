using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using AcronymLookup.Core;
using AcronymLookup.Models;
using AcronymLookup.Utilities;

namespace AcronymLookup.UI
{
    /// <summary>
    /// Modal approval queue for Moderators / Managers.
    /// Loads pending TermRequests for the current project and lets the
    /// reviewer approve or reject each one with an optional comment.
    /// </summary>
    public partial class ApprovalQueueWindow : Window
    {
        #region Private Fields

        private readonly DatabaseHandler _databaseHandler;
        private readonly int _projectId;
        private readonly int _reviewerUserId;

        // In-memory list — items are removed as they are actioned
        private List<TermRequest> _pendingRequests = new();

        #endregion

        #region Constructor

        public ApprovalQueueWindow(DatabaseHandler databaseHandler, int projectId, int reviewerUserId)
        {
            InitializeComponent();

            _databaseHandler = databaseHandler ?? throw new ArgumentNullException(nameof(databaseHandler));
            _projectId       = projectId;
            _reviewerUserId  = reviewerUserId;

            Loaded += OnWindowLoaded;
        }

        #endregion

        #region Load

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            LoadRequests();
        }

        /// <summary>
        /// Fetches pending requests from the DB and binds them to the list.
        /// </summary>
        private void LoadRequests()
        {
            try
            {
                _pendingRequests = _databaseHandler.GetPendingRequests(_projectId);

                RequestListBox.ItemsSource = null;
                RequestListBox.ItemsSource = _pendingRequests;

                UpdatePendingCount();
                UpdateEmptyState();

                Logger.Log($"Loaded {_pendingRequests.Count} pending request(s)");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error loading requests: {ex.Message}");
                MessageBox.Show(
                    $"Failed to load pending requests.\n\n{ex.Message}",
                    "Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region List Selection

        private void RequestListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (RequestListBox.SelectedItem is TermRequest selected)
            {
                PopulateDetailPanel(selected);
            }
            else
            {
                HideDetailPanel();
            }
        }

        #endregion

        #region Detail Panel

        /// <summary>
        /// Fills the right-hand detail panel with the selected request's data.
        /// </summary>
        private void PopulateDetailPanel(TermRequest request)
        {
            // Show panel, hide placeholder
            DetailPanel.Visibility    = Visibility.Visible;
            NoSelectionText.Visibility = Visibility.Collapsed;

            // Term + type badge
            DetailAbbreviation.Text = request.DisplayAbbreviation;
            DetailTypeText.Text     = request.RequestType;
            DetailTypeBadge.Background = request.RequestType switch
            {
                "Add"    => new SolidColorBrush(Color.FromRgb(40,  167, 69)),   // green
                "Edit"   => new SolidColorBrush(Color.FromRgb(0,   123, 255)),  // blue
                "Delete" => new SolidColorBrush(Color.FromRgb(220, 53,  69)),   // red
                _        => new SolidColorBrush(Color.FromRgb(108, 117, 125))   // gray fallback
            };

            // Definition — pick the right field based on request type
            DetailDefinition.Text = request.DisplayDefinition;

            // Category — show panel only if value present
            string? category = request.RequestType == "Add" ? request.NewCategory : request.EditedCategory;
            if (!string.IsNullOrWhiteSpace(category))
            {
                DetailCategory.Text              = category;
                DetailCategoryPanel.Visibility   = Visibility.Visible;
            }
            else
            {
                DetailCategoryPanel.Visibility   = Visibility.Collapsed;
            }

            // Notes — show panel only if value present
            string? notes = request.RequestType == "Add" ? request.NewNotes : request.EditedNotes;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                DetailNotes.Text               = notes;
                DetailNotesPanel.Visibility    = Visibility.Visible;
            }
            else
            {
                DetailNotesPanel.Visibility    = Visibility.Collapsed;
            }

            // Reason
            if (!string.IsNullOrWhiteSpace(request.RequestReason))
            {
                DetailReason.Text             = request.RequestReason;
                DetailReasonPanel.Visibility  = Visibility.Visible;
            }
            else
            {
                DetailReasonPanel.Visibility  = Visibility.Collapsed;
            }

            // Requester + date
            DetailRequester.Text = request.RequestedByUserName;
            DetailDate.Text      = request.DateRequested.ToString("MMMM d, yyyy");

            // Clear previous comment
            ReviewCommentInput.Text = string.Empty;

            // Footer status
            FooterStatusText.Text = string.Empty;
        }

        private void HideDetailPanel()
        {
            DetailPanel.Visibility     = Visibility.Collapsed;
            NoSelectionText.Visibility = Visibility.Visible;
            FooterStatusText.Text      = string.Empty;
        }

        #endregion

        #region Approve / Reject

        private void ApproveButton_Click(object sender, RoutedEventArgs e)
        {
            if (RequestListBox.SelectedItem is not TermRequest selected) return;

            var confirm = MessageBox.Show(
                $"Approve the {selected.RequestType.ToLower()} request for '{selected.DisplayAbbreviation}'?",
                "Confirm Approval",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                string? comment = ReviewCommentInput.Text.Trim();
                bool success = _databaseHandler.ApproveRequest(
                    selected.TermRequestID,
                    _reviewerUserId,
                    string.IsNullOrWhiteSpace(comment) ? null : comment);

                if (success)
                {
                    Logger.Log($"Approved request {selected.TermRequestID} for '{selected.DisplayAbbreviation}'");
                    RemoveRequestFromList(selected);
                    FooterStatusText.Text = $"Approved '{selected.DisplayAbbreviation}'.";
                }
                else
                {
                    MessageBox.Show(
                        "Failed to approve the request. Please try again.",
                        "Approval Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error approving request: {ex.Message}");
                MessageBox.Show(
                    $"Error approving request:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RejectButton_Click(object sender, RoutedEventArgs e)
        {
            if (RequestListBox.SelectedItem is not TermRequest selected) return;

            var confirm = MessageBox.Show(
                $"Reject the {selected.RequestType.ToLower()} request for '{selected.DisplayAbbreviation}'?",
                "Confirm Rejection",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                string? comment = ReviewCommentInput.Text.Trim();
                bool success = _databaseHandler.RejectRequest(
                    selected.TermRequestID,
                    _reviewerUserId,
                    string.IsNullOrWhiteSpace(comment) ? null : comment);

                if (success)
                {
                    Logger.Log($"Rejected request {selected.TermRequestID} for '{selected.DisplayAbbreviation}'");
                    RemoveRequestFromList(selected);
                    FooterStatusText.Text = $"Rejected '{selected.DisplayAbbreviation}'.";
                }
                else
                {
                    MessageBox.Show(
                        "Failed to reject the request. Please try again.",
                        "Rejection Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error rejecting request: {ex.Message}");
                MessageBox.Show(
                    $"Error rejecting request:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Removes a request from the in-memory list and refreshes the ListBox.
        /// If the list becomes empty the empty state is shown.
        /// </summary>
        private void RemoveRequestFromList(TermRequest request)
        {
            _pendingRequests.Remove(request);

            // Rebind
            RequestListBox.ItemsSource = null;
            RequestListBox.ItemsSource = _pendingRequests;

            UpdatePendingCount();
            UpdateEmptyState();
            HideDetailPanel();
        }

        private void UpdatePendingCount()
        {
            int count = _pendingRequests.Count;
            PendingCountText.Text      = count == 1 ? "1 pending" : $"{count} pending";
            CountBadge.Background      = count == 0
                ? new SolidColorBrush(Color.FromRgb(108, 117, 125))  // gray when empty
                : new SolidColorBrush(Color.FromRgb(0,   123, 255)); // blue when items exist
        }

        private void UpdateEmptyState()
        {
            bool isEmpty = _pendingRequests.Count == 0;
            EmptyStateText.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        #endregion
    }
}