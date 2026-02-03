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
        private bool _canAddToProject; 
        private List<UserProjectInfo> _availableProjects = new List<UserProjectInfo>(); 
        private UserProjectInfo? _currentDefaultProject = null; 

        #endregion

        #region Constructor 

        public AddTermWindow(string? searchTerm = null, bool canAddToProject = false, List<UserProjectInfo>? availableProjects = null, UserProjectInfo? currentProject = null)
        {
            InitializeComponent();

            _initialSearchTerm = searchTerm;
            _canAddToProject = canAddToProject;
            _availableProjects = availableProjects ?? new List<UserProjectInfo>(); 
            _currentDefaultProject = currentProject; 

            //if a search term provided, pre-fill the abbreviation field 
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                AbbreviationInput.Text = searchTerm;
                //move focus to abbreviation input 
                AbbreviationInput.Focus();
            }

            ConfigureDatabaseSelection();
            ConfigureProjectSelection(); 

            Logger.Log("Add Term window opened"); 
        }


        #endregion

        #region Database Selection

        /// <summary>
        /// Configures the database selector based on user permissions
        /// </summary>
        private void ConfigureDatabaseSelection()
        {
            if (_canAddToProject)
            {
                // User can add to project - both options available
                DatabaseSelector.IsEnabled = true;
                PermissionWarning.Visibility = Visibility.Collapsed;
                Logger.Log("User can add to both Personal and Project databases");
            }
            else
            {
                // User can't add to project directly - show warning
                DatabaseSelector.SelectedIndex = 0; // Force "Personal" selection
                PermissionWarning.Visibility = Visibility.Visible;
                
                // Disable the "Project" option
                var projectItem = (ComboBoxItem)DatabaseSelector.Items[1];
                projectItem.IsEnabled = false;
                
                Logger.Log("User can only add to Personal database (no project permissions)");
            }
        }

        private void ConfigureProjectSelection()
        {
            if(_availableProjects.Count > 0)
            {
                ProjectSelector.ItemsSource = _availableProjects;

                if(_currentDefaultProject != null)
                {
                    ProjectSelector.SelectedItem = _currentDefaultProject;
                }
                else
                {
                    ProjectSelector.SelectedIndex = 0; 
                }

                Logger.Log($"Configured project selector with {_availableProjects.Count} projects");

            }
            else
            {
                Logger.Log("No projects available for selection"); 
            }
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
                string definition = DefinitionInput.Text.Trim();
                string category = CategoryInput.Text.Trim();
                string notes = NotesInput.Text.Trim();

                var selectedItem = (ComboBoxItem)DatabaseSelector.SelectedItem;
                string selectedDatabase = selectedItem.Tag.ToString() ?? "Personal";
                
                Logger.Log($"Attempting to save term to {selectedDatabase} database");


                var args = new TermAddedEventArgs(abbreviation, definition, category, notes, selectedDatabase, selectedProjectId);
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

        private void DatabaseSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedItem = (ComboBoxItem)DatabaseSelector_SelectionChanged.SelectedItem; 
                string selectedDatabase = selectedItem?.Tag?.ToString() ?? "Personal"; 

                if (selectedDatabase == "Project" && _canAddToProject)
                {
                    ProjectSelectorPanel.Visibility = Visibility.Visible;
                    Logger.Log("Project selector shown");
                }
                else
                {
                    ProjectSelectorPanel.Visibility = Visibility.Collapsed;
                    Logger.Log("Project selector hidden");
                }
            }catch (Exception ex)
            {
                Logger.Log($"Error in DatabaseSelector_SelectionChanged: {ex.Message}"); 
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
            public string TargetDatabase { get; }
            public int? TargetProjectId { get; }

            public TermAddedEventArgs(string abbreviation, string definition, string category, string notes, string targetDatabase, int? targetProjectId = null)
            {
                Abbreviation = abbreviation;
                Definition = definition;
                Category = category;
                Notes = notes;
                TargetDatabase = targetDatabase;
                TargetProjectId = targetProjectId; 
            }
        }

        #endregion
    }
}
