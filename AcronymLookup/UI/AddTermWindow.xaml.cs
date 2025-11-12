using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AcronymLookup.Utilities; 

namespace AcronymLookup.UI
{
    /// <summary>
    /// Interaction logic for AddTermWindow.xaml
    /// </summary>
    public partial class AddTermWindow : Window
    {

        #region Events 

        public event EventHandler<TermAddedEventArgs>? TermAdded;

        #endregion


        #region Properties 

        private string? _initialSearchTerm;

        #endregion

        #region Constructor 

        public AddTermWindow(string? searchTerm = null)
        {
            InitializeComponent();

            _initialSearchTerm = searchTerm;

            //if a search term provided, pre-fill the abbreviation field 
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                AbbreviationInput.Text = searchTerm;

                //move focus to abbreviation input 
                AbbreviationInput.Focus();
            }
            Logger.Log("Add Term window opened"); 
        }


        #endregion

        #region EventHandlers 
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //validate input before saving 
                if (!ValidateInput())
                {
                    return;
                }

                //get the valuses from input fields 
                string abbreviation = AbbreviationInput.Text.Trim();
                string definition = AbbreviationInput.Text.Trim();
                string category = CategoryInput.Text.Trim();
                string notes = NotesInput.Text.Trim();

                Logger.Log($"Attempting to save term");

                var args = new TermAddedEventArgs(abbreviation, definition, category, notes);
                TermAdded?.Invoke(this, args);

                Logger.Log("Term saved successfully!");

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error saving term: {ex.Message}");
                ShowValidationMessage($"Error saving term: {ex.Message}");
            }
        }

        private void CancelButton_Click( object sender, RoutedEventArgs e)
        {
            Logger.Log("Add term cancelled by user");
            this.DialogResult = false;
            this.Close(); 
        }

        #endregion

        #region Validation 

        private bool ValidateInput()
        {

            if (string.IsNullOrWhiteSpace(AbbreviationInput.Text))
            {
                ShowValidationMessage("Abbreviation cannot be empty");
                AbbreviationInput.Focus();
                return false; 
            }

            //check if definition is empty 
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
            Logger.Log($"Validation message: {message}");
        }

        private void HideValidationMessage()
        {
            ValidationMessage.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region EventArgs 


        public class TermAddedEventArgs : EventArgs
        {
            public string Abbreviation { get; }
            public string Definition { get; }
            public string Category { get; } 
            public string Notes { get; } 

            public TermAddedEventArgs(string abbreviation, string definition, string category, string notes)
            {
                Abbreviation = abbreviation;
                Definition = definition;
                Category = category;
                Notes = notes;
            }
        }

        #endregion
    }
}
