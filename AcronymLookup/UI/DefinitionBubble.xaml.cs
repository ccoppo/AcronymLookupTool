using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls; 
using AcronymLookup.Models;
using AcronymLookup.Utilities; 

namespace AcronymLookup.UI
{
    public partial class DefinitionBubble : Window
    {
        private List<AbbreviationData> _currentDefinitions;
        private int _currentDefinitionIndex;
        private string _searchTerm;
        private string _currentProjectName = "Unknown Project";
        private List<UserProjectInfo> _availableProjects = new List<UserProjectInfo>();

        #region Events 

        public event EventHandler<AddTermEventArgs>? AddTermRequested;

        public event EventHandler? BubbleClosed;

        public event EventHandler<EditTermEventArgs>? EditTermRequested; 

        public event EventHandler <DeleteTermEventArgs>? DeleteTermRequested;
        public event EventHandler <PromoteTermEventArgs>? PromoteTermRequested;  

        #endregion

        public DefinitionBubble()
        {
            InitializeComponent();

            _currentDefinitions = new List<AbbreviationData>();
            _currentDefinitionIndex = 0; 
            _searchTerm = string.Empty;

            PositionBubbleSafely();

            Logger.Log("Definition bubble created"); 
        }

        #region Public Methods 

        public void ShowDefinition(string searchTerm, List<AbbreviationData> definitions, string currentProjectName, List<UserProjectInfo> availableProjects)
        {
            try
            {
                Logger.Log($"Showing definition for: '{searchTerm}'");

                _searchTerm = searchTerm;
                _currentDefinitions = definitions ?? new List<AbbreviationData>();
                _currentDefinitionIndex = 0;
                _currentProjectName = currentProjectName;
                _availableProjects = availableProjects ?? new List<UserProjectInfo>();

                if (_currentDefinitions.Any())
                {
                    DisplayCurrentDefinition();
                    UpdateNavigationButtons();

                    //hide not found container 
                    NotFoundContainer.Visibility = Visibility.Collapsed;

                    Logger.Log($"Displaying {_currentDefinitions.Count} definitions");
                }
                else
                {
                    //show not found 
                    ShowNotFoundMessage();

                    Logger.Log("No definitions found");
                }

                this.Show();
                this.Activate();

                //PositionNearCursor();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error showing definition: {ex.Message}");
                ShowErrorMessage($"Error displaying definition: {ex.Message}");
            }
        }

        public void CloseBubble()
        {
            try
            {
                Logger.Log("Closing definition bubble");
                this.Hide();
                BubbleClosed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error closing bubble: {ex.Message}");
            }
        }

        #endregion

        #region Private Display Methods 

        private void DisplayCurrentDefinition()
        {
            if (!_currentDefinitions.Any() || _currentDefinitionIndex >= _currentDefinitions.Count)
                return; 

            var definition = _currentDefinitions[ _currentDefinitionIndex ];

            //show main content 
            AbbreviationText.Text = definition.Abbreviation;
            DefinitionText.Text = definition.Definition; 

            if (!string.IsNullOrWhiteSpace(definition.Source))
            {
                
                //color code: green personal, blue project
                if (definition.Source == "Personal")
                {
                    SourceText.Text = "Personal";
                    SourceBadge.Background = new System.Windows.Media.SolidColorBrush( 
                        System.Windows.Media.Color.FromRgb(40, 167, 69)
                    );
                    PromoteButton.Visibility = Visibility.Visible;
                    PromoteButton.IsEnabled = true;
                }
                else if (definition.Source == "Project")
                {
                    SourceText.Text = _currentProjectName; 
                    SourceBadge.Background = new System.Windows.Media.SolidColorBrush( 
                        System.Windows.Media.Color.FromRgb(0, 123, 255)
                    );
                    PromoteButton.Visibility = Visibility.Collapsed;
                }
                SourceBadge.Visibility = Visibility.Visible; 
            }
            else
            {
                SourceBadge.Visibility = Visibility.Collapsed; 
            }

            //show category if present 
            if (!string.IsNullOrWhiteSpace(definition.Category))
            {
                CategoryText.Text = $"Category: {definition.Category}";
                CategoryText.Visibility = Visibility.Visible; 
            }
            else
            {
                CategoryText.Visibility = Visibility.Collapsed; 
            }

            //show notes if available 
            if (definition.HasNotes)
            {
                NotesText.Text = definition.Notes;
                NotesContainer.Visibility = Visibility.Visible; 
            }
            else
            {
                NotesContainer.Visibility = Visibility.Visible; 
            }
            Logger.Log($"Displayed definition {_currentDefinitionIndex + 1}/{_currentDefinitions.Count}"); 
        }

        private void ShowNotFoundMessage()
        {
            //hide main content 
            AbbreviationText.Visibility = Visibility.Collapsed;
            DefinitionText.Visibility = Visibility.Collapsed;
            CategoryText.Visibility = Visibility.Collapsed;
            NotesContainer.Visibility = Visibility.Collapsed;
            SourceBadge.Visibility = Visibility.Collapsed; 

            //show not found
            SearchTermText.Text = $"Searched For: {_searchTerm}";
            NotFoundContainer.Visibility = Visibility.Visible;

            //update navigation buttons 
            UpdateNavigationButtons(); 
        }

        private void ShowErrorMessage(string message )
        {
            AbbreviationText.Text = "ERROR";
            DefinitionText.Text = message;
            CategoryText.Visibility = Visibility.Collapsed;
            NotesContainer.Visibility = Visibility.Collapsed;
            NotFoundContainer.Visibility = Visibility.Collapsed;
            SourceBadge.Visibility = Visibility.Collapsed; 

            this.Show(); 
        }

        #endregion

        #region Nav methods

        private void UpdateNavigationButtons()
        {
            bool hasMultiple = _currentDefinitions.Count > 1; 
            bool hasDefinitions = _currentDefinitions.Any();

            //enable/disable navigation buttons 
            PreviousButton.IsEnabled = hasMultiple && _currentDefinitionIndex > 0;
            NextButton.IsEnabled = hasMultiple && _currentDefinitionIndex < _currentDefinitions.Count - 1;

            //Always enable the add button 
            AddButton.IsEnabled = true;

            //update tooltips 
            if(hasDefinitions && hasMultiple)
            {
                PreviousButton.ToolTip = $"Previous definition({_currentDefinitionIndex + 1}/{_currentDefinitions.Count})";
                NextButton.ToolTip = $"Next definition ({_currentDefinitionIndex + 1}/{_currentDefinitions.Count})"; 
            } else
            {
                PreviousButton.ToolTip = "Previous definition";
                NextButton.ToolTip = "Next definition"; 
            }
        }

        private void ShowPreviousDefinition()
        {
            if(_currentDefinitionIndex > 0)
            {
                _currentDefinitionIndex--;
                DisplayCurrentDefinition();
                UpdateNavigationButtons();

                Logger.Log($"Showing next definition: {_currentDefinitionIndex + 1}/{_currentDefinitions.Count}"); 
            }
        }

        private void ShowNextDefinition()
        {
            if (_currentDefinitionIndex < _currentDefinitions.Count - 1)
            {
                _currentDefinitionIndex++;
                DisplayCurrentDefinition();
                UpdateNavigationButtons();

                Logger.Log($"Showing next definition: {_currentDefinitionIndex + 1}/{_currentDefinitions.Count}");
            }
        }

        #endregion

        #region Positioning Methods 

        private void PositionBubbleSafely()
        {
            try
            {
                //get screen dimension
                double screenWidth = SystemParameters.PrimaryScreenWidth; 
                double screenHeight = SystemParameters.PrimaryScreenHeight;

                //default position = ~center
                double defaultLeft = screenWidth * 0.6;
                double defaultTop = screenWidth * 0.3;

                //ensure bubble fits on screen
                if (defaultLeft + this.Width > screenWidth)
                    defaultLeft = screenWidth - this.Width - 20;

                if (defaultTop + this.Height > screenHeight)
                    defaultTop = screenHeight - this.Height - 20;

                //make sure theres distance from edges 
                if (defaultLeft < 20) defaultLeft = 20;
                if(defaultTop < 20) defaultTop = 20;

                this.Left = defaultLeft;
                this.Top = defaultTop;

                Logger.Log($"Positioned bubble at({defaultLeft: F0}, {defaultTop:F0})"); 

            }catch (Exception ex)
            {
                Logger.Log("Error positioning bubble: " + ex.Message);

                //fallback position 
                this.Left = 100;
                this.Top = 100; 
            }
        }

        //todo: add cursor positioning functions to position the bubble next to user workflow 

        #endregion

        #region Event Handlers 

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseBubble(); 
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPreviousDefinition(); 
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            ShowNextDefinition(); 
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.Log("Add term button clicked");

                var args = new AddTermEventArgs(_searchTerm, _currentDefinitions.Any());
                AddTermRequested?.Invoke(this, args); 
            }catch (Exception ex)
            {
                Logger.Log("Error handling add button: " + ex.Message); 
            }
        }

        private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    this.DragMove();
                    Logger.Log("Bubble moved by user"); 
                }
            }catch (Exception ex)
            {
                Logger.Log("Error during drag: " + ex.Message); 
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_currentDefinitions.Any() || _currentDefinitionIndex >= _currentDefinitions.Count)
                {
                    Logger.Log("No term to edit");
                    return;
                }

                var currentTerm = _currentDefinitions[_currentDefinitionIndex];
                Logger.Log($"Edit button clicked for '{currentTerm.Abbreviation}'");

                var args = new EditTermEventArgs(currentTerm);
                EditTermRequested?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling edit button: {ex.Message}");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_currentDefinitions.Any() || _currentDefinitionIndex >= _currentDefinitions.Count)
                {
                    Logger.Log("No term to delete");
                    return;
                }

                var currentTerm = _currentDefinitions[_currentDefinitionIndex];
                Logger.Log($"Delete button clicked for '{currentTerm.Abbreviation}'");

                // Show confirmation dialog
                var result = MessageBox.Show(
                    $"Are you sure you want to delete '{currentTerm.Abbreviation}'?\n\n" +
                    $"Definition: {currentTerm.Definition}\n\n" +
                    $"This action cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    Logger.Log($"User confirmed deletion of '{currentTerm.Abbreviation}'");
                    var args = new DeleteTermEventArgs(currentTerm);
                    DeleteTermRequested?.Invoke(this, args);
                }
                else
                {
                    Logger.Log("User cancelled deletion");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling delete button: {ex.Message}");
            }
        }

        private void PromoteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_currentDefinitions.Any() || _currentDefinitionIndex >= _currentDefinitions.Count)
                {
                    Logger.Log("No term to promote");
                    return;
                }

                var currentTerm = _currentDefinitions[_currentDefinitionIndex];
                
                //only allow promoting Personal terms
                if (currentTerm.Source != "Personal")
                {
                    Logger.Log($"Cannot promote - term is not from Personal database (Source: {currentTerm.Source})");
                    MessageBox.Show(
                        "Only terms from your Personal database can be promoted to Projects.",
                        "Cannot Promote",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                Logger.Log($"Promote button clicked for '{currentTerm.Abbreviation}'");

                var args = new PromoteTermEventArgs(currentTerm);
                PromoteTermRequested?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling promote button: {ex.Message}");
            }
        }


        protected override void OnClosed(EventArgs e)
        {
            BubbleClosed?.Invoke(this, EventArgs.Empty);
            base.OnClosed(e); 
        }

        #endregion

        //todo: windows api cursor positions
        //

        #region Event Args classes 
        public class AddTermEventArgs : EventArgs
        {
            public string SearchTerm { get; } 
            public bool HasExistingDefinitions { get; }

            public AddTermEventArgs(string searchTerm, bool hasExistingDefinitions)
            {
                SearchTerm = searchTerm;
                HasExistingDefinitions = hasExistingDefinitions; 
            }
            
        }

        public class EditTermEventArgs : EventArgs
        {
            public AbbreviationData Term { get; }

            public EditTermEventArgs(AbbreviationData term)
            {
                Term = term;
            }
        }

        public class DeleteTermEventArgs : EventArgs
        {
            public AbbreviationData Term { get; }

            public DeleteTermEventArgs(AbbreviationData term)
            {
                Term = term;
            }
        }

        public class PromoteTermEventArgs : EventArgs
        {
            public AbbreviationData Term { get; }

            public PromoteTermEventArgs(AbbreviationData term)
            {
                Term = term;
            }
        }

        

        #endregion
    }
}
