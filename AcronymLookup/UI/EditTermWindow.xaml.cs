using System;
using System.Windows;
using AcronymLookup.Models;
using AcronymLookup.Utilities;

namespace AcronymLookup.UI
{
    /// <summary>
    /// Window for editing existing terms
    /// Pre-fills form with current values
    /// </summary>
    public partial class EditTermWindow : Window
    {
        #region Events
        public event EventHandler<TermEditedEventArgs>? TermEdited;
        #endregion

        #region Properties
        private readonly AbbreviationData _originalTerm;
        private readonly bool _isPersonalTerm;
        #endregion

        #region Constructor
        public EditTermWindow(AbbreviationData term, bool isPersonalTerm = false)
        {
            InitializeComponent();

            _originalTerm = term ?? throw new ArgumentNullException(nameof(term));
            _isPersonalTerm = isPersonalTerm;

            // Pre-fill the form with current values
            AbbreviationInput.Text = term.Abbreviation;
            DefinitionInput.Text = term.Definition;
            CategoryInput.Text = term.Category ?? "";
            NotesInput.Text = term.Notes ?? "";

            // Update title based on term type
            this.Title = isPersonalTerm ? "Edit Personal Term" : "Edit Project Term";

            // Focus on definition (most likely field to edit)
            DefinitionInput.Focus();
            DefinitionInput.SelectAll();

            Logger.Log($"Edit window opened for '{term.Abbreviation}' ({(isPersonalTerm ? "Personal" : "Project")})");
        }
        #endregion

        #region Event Handlers
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate input
                if (!ValidateInput())
                {
                    return;
                }

                // Get values
                string abbreviation = AbbreviationInput.Text.Trim();
                string newDefinition = DefinitionInput.Text.Trim();
                string newCategory = CategoryInput.Text.Trim();
                string newNotes = NotesInput.Text.Trim();

                // Check if anything actually changed
                if (newDefinition == _originalTerm.Definition &&
                    newCategory == (_originalTerm.Category ?? "") &&
                    newNotes == (_originalTerm.Notes ?? ""))
                {
                    Logger.Log("No changes detected - closing window");
                    MessageBox.Show(
                        "No changes were made.",
                        "No Changes",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    this.DialogResult = true;
                    this.Close();
                    return;
                }

                Logger.Log($"Changes detected for '{abbreviation}'");

                // Raise event with new values
                var args = new TermEditedEventArgs(
                    abbreviation,
                    newDefinition,
                    newCategory,
                    newNotes,
                    _isPersonalTerm);

                TermEdited?.Invoke(this, args);

                Logger.Log("Term edited successfully");

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error saving changes: {ex.Message}");
                ShowValidationMessage($"Error saving changes: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Edit cancelled by user");
            this.DialogResult = false;
            this.Close();
        }
        #endregion

        #region Validation
        private bool ValidateInput()
        {
            // Check if definition is empty
            if (string.IsNullOrWhiteSpace(DefinitionInput.Text))
            {
                ShowValidationMessage("Definition cannot be empty");
                DefinitionInput.Focus();
                return false;
            }

            HideValidationMessage();
            return true;
        }

        private void ShowValidationMessage(string message)
        {
            ValidationMessage.Text = message;
            ValidationMessage.Visibility = Visibility.Visible;
            Logger.Log($"Validation error: {message}");
        }

        private void HideValidationMessage()
        {
            ValidationMessage.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region Event Args
        public class TermEditedEventArgs : EventArgs
        {
            public string Abbreviation { get; }
            public string NewDefinition { get; }
            public string NewCategory { get; }
            public string NewNotes { get; }
            public bool IsPersonalTerm { get; }

            public TermEditedEventArgs(
                string abbreviation,
                string newDefinition,
                string newCategory,
                string newNotes,
                bool isPersonalTerm)
            {
                Abbreviation = abbreviation;
                NewDefinition = newDefinition;
                NewCategory = newCategory;
                NewNotes = newNotes;
                IsPersonalTerm = isPersonalTerm;
            }
        }
        #endregion
    }
}