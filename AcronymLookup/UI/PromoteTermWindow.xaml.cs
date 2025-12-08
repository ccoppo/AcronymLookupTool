using System;
using System.Windows;
using AcronymLookup.Models;
using AcronymLookup.Utilities;

namespace AcronymLookup.UI
{
    public partial class PromoteTermWindow : Window
    {
        #region Events
        public event EventHandler<TermPromotedEventArgs>? TermPromoted;
        #endregion

        #region Properties
        private readonly AbbreviationData _term;
        private readonly bool _canAddDirectly;
        #endregion

        #region Constructor
        public PromoteTermWindow(AbbreviationData term, bool canAddDirectly)
        {
            InitializeComponent();

            _term = term ?? throw new ArgumentNullException(nameof(term));
            _canAddDirectly = canAddDirectly;

            // Set term info
            TermInfoText.Text = $"Term: {term.Abbreviation} - {term.Definition}";

            // Update message based on permissions
            if (_canAddDirectly)
            {
                PermissionInfoText.Text = "You have permission to add this directly to the project database.";
            }
            else
            {
                PermissionInfoText.Text = "This will create a request for approval by project managers.";
            }

            Logger.Log($"Promote window opened for '{term.Abbreviation}'");
        }
        #endregion

        #region Event Handlers
        private void PromoteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string reason = ReasonInput.Text.Trim();

                Logger.Log($"Promoting term '{_term.Abbreviation}' with reason: '{reason}'");

                var args = new TermPromotedEventArgs(_term.Abbreviation, reason);
                TermPromoted?.Invoke(this, args);

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error promoting term: {ex.Message}");
                ShowValidationMessage($"Error: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Promote cancelled by user");
            this.DialogResult = false;
            this.Close();
        }
        #endregion

        #region Validation
        private void ShowValidationMessage(string message)
        {
            ValidationMessage.Text = message;
            ValidationMessage.Visibility = Visibility.Visible;
        }
        #endregion

        #region Event Args
        public class TermPromotedEventArgs : EventArgs
        {
            public string Abbreviation { get; }
            public string Reason { get; }

            public TermPromotedEventArgs(string abbreviation, string reason)
            {
                Abbreviation = abbreviation;
                Reason = reason;
            }
        }
        #endregion
    }
}