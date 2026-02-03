using AcronymLookup.Core;
using AcronymLookup.Failed_tries;
using AcronymLookup.Models;
using AcronymLookup.Services;
using AcronymLookup.UI; 
using AcronymLookup.Utilities;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration; 
using System.Windows;
using static AcronymLookup.Services.ClipboardHandler;
using static AcronymLookup.UI.DefinitionBubble;
using static AcronymLookup.UI.AddTermWindow; 
using static AcronymLookup.UI.EditTermWindow;
using static AcronymLookup.UI.PromoteTermWindow; 
//using AcronymLookup.UI; 

namespace AcronymLookup
{

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        #region Private Fields 
        //for output console

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole(); 

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        

        private HotkeyManager? _hotkeyManager;
        private ClipboardHandler? _clipboardHandler;
        //private CsvParser? _csvParser;
        private DatabaseHandler? _databaseHandler; 
        private DefinitionBubble? _currentBubble; 

        private PermissionService? _permissionService;
        private AuditService? _auditService;
        private PersonalDatabaseService? _personalDatabaseService;
        private SearchService? _searchService;
        private List<UserProjectInfo> _userProjects = new List<UserProjectInfo>();
        private UserProjectInfo? _currentProject = null;




        #endregion


        #region Application Lifecycle 
        /// <summary>
        /// Called when the application starts up 
        /// This replaces the normal main window startup
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            //Console for debugging 
            AllocConsole();
            Console.Title = "AcronymLookup Debug Console";

            try
            {

                Logger.Log("AcronymLookup application starting up...");

                // Initialize all services
                InitializeServices();

                // Connect services together
                ConnectServices();

                // Load CSV data
                TestDatabaseConnection();

                Logger.Log("Application startup complete!");
                Logger.Log("Workflow: Select text -> Ctrl+C -> Ctrl+Alt+L");

            }
            catch (Exception ex)
            {
                Logger.Log($"Startup error: {ex.Message}");
                MessageBox.Show($"Error starting application: {ex.Message}",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();

            }
        }

        /// <summary> 
        /// Called when the application is shutting down 
        /// Well use this later to clean up resources 
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            Console.WriteLine("Acronym Lookup application shutting down"); 

            try
            {
                // Clean up services in reverse order
                CleanupServices();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error during shutdown: {ex.Message}");
            }

            base.OnExit(e);

        }
        #endregion


        #region Service Initialization
        
        private void InitializeServices()
        {
            Logger.Log("Initializing Services");

            //get connection string from user secrets
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<App>()
                .Build();
            string connectionString = configuration.GetConnectionString("AzureDatabase")
                ?? throw new InvalidOperationException("Connection string not found in user secrets");

            //Initialize AuditService (needed by DatabaseHandler) 
            _auditService = new AuditService(connectionString);
            Logger.Log("Initialized AuditService");

            //Initialize DatabaseHandler (now requires AuditService) 
            _databaseHandler = new DatabaseHandler(connectionString, _auditService);
            Logger.Log("Initialized DatabaseHandler");

            //Initialize PermissionService 
            _permissionService = new PermissionService(connectionString);
            Logger.Log("Initialized PermissionService");

            //4. Initialize PersonalDatabaseService
            _personalDatabaseService = new PersonalDatabaseService(connectionString);
            Logger.Log("Initialized PersonalDatabaseService");

            //5. Initialize SearchService (requires DatabaseHandler and PersonalDatabaseService)
            _searchService = new SearchService(_databaseHandler, _personalDatabaseService);
            Logger.Log("Initialized SearchService");

            // 6. Initialize Clipboard Handler
            _clipboardHandler = new ClipboardHandler();
            Logger.Log("Initialized Clipboard Handler");

            // 7. Initialize Hotkey Manager
            _hotkeyManager = new HotkeyManager();
            Logger.Log("Initialized Hotkey manager");
        }


        /// <summary>
        /// Connects services with their event handlers
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void ConnectServices()
        {
            Logger.Log("Connecting services...");

            if (_hotkeyManager != null && _clipboardHandler != null && _databaseHandler != null)
            {
                // Connect: Hotkey Press → Start Clipboard Capture
                _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
                Logger.Log("Hotkey → Clipboard capture connected");

                // Connect: Text Captured → Lookup Abbreviation  
                _clipboardHandler.TextCaptured += OnTextCaptured;
                Logger.Log("Clipboard → Abbreviation lookup connected");

                // Register the hotkey
                bool hotkeyRegistered = _hotkeyManager.RegisterHotkey();
                if (hotkeyRegistered)
                {
                    Logger.Log("Ctrl+Alt+L hotkey registered successfully");
                }
                else
                {
                    Logger.Log("Failed to register Ctrl+Alt+L hotkey");
                    MessageBox.Show("Could not register Ctrl+Alt+L hotkey. It may be in use by another application.",
                        "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

            }
            else
            {
                throw new InvalidOperationException("Services not properly initialized");
            }
        }

        private void TestDatabaseConnection()
        {
            Logger.Log("Testing database connection...");

            if (_databaseHandler != null)
            {
                bool connected = _databaseHandler.TestConnection();

                if (connected)
                {

                    string windowsUsername = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                    Logger.Log($"current windows user: {windowsUsername}");

                    int? userId = _databaseHandler.GetUserIdByWindowsUsername(windowsUsername);

                    if (userId.HasValue)
                    {
                        Logger.Log($"Found user in database: UserID = {userId.Value}");

                        _userProjects = _databaseHandler.GetUserProjects(userId.Value);

                        if (_userProjects.Count > 0)
                        {
                            _currentProject = _userProjects[0];
                    
                            _databaseHandler.SetUserContext(userId.Value, _currentProject.ProjectID);

                            int count = _databaseHandler.Count;
                            Logger.Log("Successfully connected to database");
                            Logger.Log($"User has access to {_userProjects.Count} project(s):");
                            
                            //Log all available projects
                            foreach (var project in _userProjects)
                            {
                                string marker = (project.ProjectID == _currentProject.ProjectID) ? "→ " : "  ";
                                Logger.Log($"{marker}{project.DisplayName} (Role: {project.UserRole})");
                            }
                            
                            Logger.Log($"Current project: {_currentProject.DisplayName}");
                            Logger.Log($"Database has {count} total abbreviations");
                        }
                        else
                        {
                            Logger.Log("User has no projects assigned");
                            MessageBox.Show(
                                "you are not assigned to any projects, please contact administrator",
                                "no projects found",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            Shutdown();
                        }
                    }
                    else
                    {
                        Logger.Log($"User not found in database: {windowsUsername}");
                        MessageBox.Show(
                            "your windows account is not registered in the system",
                            "user not found",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        Shutdown();
                    }
                }
                else
                {
                    Logger.Log($"Failed to connect to database");
                    MessageBox.Show("Could not connect to Azure SQL Database. Please check your connection string.", "Database Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                }
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles hotkey press events (Ctrl+Alt+L pressed)
        /// WORKFLOW: Hotkey → Trigger clipboard capture
        /// </summary>
        private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
        {
            try
            {
                Logger.Log("Hotkey pressed! Starting text capture...");

                if (_clipboardHandler != null)
                {
                    // Start the clipboard capture process
                    _clipboardHandler.ProcessClipboardText();
                }
                else
                {
                    Logger.Log("Clipboard handler not available");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling hotkey press: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles text captured events (clipboard capture completed)
        /// WORKFLOW: Text captured → Lookup abbreviation → Show result
        /// </summary>
        private void OnTextCaptured(object? sender, TextCapturedEventArgs e)
        {
            try
            {
                if (e.Success && !string.IsNullOrWhiteSpace(e.CapturedText))
                {
                    Logger.Log($"Text captured: '{e.CapturedText}' - looking up...");
                    Logger.Log($"Text length: {e.CapturedText.Length}, First char code: {(int)e.CapturedText[0]}, Last char code: {(int)e.CapturedText[e.CapturedText.Length - 1]}"); 

                    // Look up the abbreviation
                    LookupAbbreviation(e.CapturedText);
                }
                else
                {
                    Logger.Log($"Text capture failed: {e.ErrorMessage}");

                    // Show error to user
                    ShowLookupResult(null, e.ErrorMessage ?? "Failed to capture text", null);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling captured text: {ex.Message}");
                ShowLookupResult(null, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region Abbreviation Lookup

        /// <summary>
        /// looks up abbreviation in database 
        /// </summary>
        /// <param name="searchTerm"></param>
        private void LookupAbbreviation(string searchTerm)
        {
            try
            {
                if (_searchService != null && _databaseHandler != null)
                {
                    Logger.Log($"Looking up: '{searchTerm}'");

                    // Search ALL (personal + project)
                    var searchResult = _searchService.SearchAllProjects(
                        searchTerm, 
                        _databaseHandler.CurrentUserId, 
                        _userProjects);

                    if (searchResult.HasResults)
                    {
                        // Convert search results to AbbreviationData list
                        var definitions = SearchService.GetAllResultsAsList(searchResult);
                        
                        Logger.Log($"Found {definitions.Count} definition(s):");
                        foreach (var def in definitions)
                        {
                            Logger.Log($"  - {def.Abbreviation}: {def.Definition}");
                        }

                        ShowDefinitionBubble(searchTerm, definitions); 
                    }
                    else
                    {
                        Logger.Log($"Not found: '{searchTerm}'");
                        ShowLookupResult(null, $"'{searchTerm}' not found in any database", searchTerm);
                    }
                }
                else
                {
                    Logger.Log("SearchService or DatabaseHandler not available");
                    ShowLookupResult(null, "Service not initialized", null);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error during lookup: {ex.Message}");
                ShowLookupResult(null, $"Lookup error: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Shows the lookup result to the user
        /// TODO: Later this will show the bubble UI - for now just console + message box
        /// </summary>
        private void ShowLookupResult(AbbreviationData? result, string? errorMessage, string? searchTerm = null)
        {
            try
            {
                //close any bubble 
                CloseBubbleIfOpen(); 

                if (result != null)
                {
                    var definitions = new List<AbbreviationData>() { result };

                    Logger.Log($"Showing result now: {result}");
                    ShowDefinitionBubble(result.Abbreviation, definitions); 
                }
                else
                {
                    Logger.Log($"showing error to user: {errorMessage}");

                    var emptyList = new List<AbbreviationData>();
                    string displayTerm = searchTerm ?? "Unknown Term";
                    ShowDefinitionBubble(displayTerm, emptyList); 
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error showing result: {ex.Message}");
            }
        }

        #endregion

        #region BubbleManagement

        private void ShowDefinitionBubble(string searchTerm, List<AbbreviationData> definitions)
        {
            try
            {
                _currentBubble = new DefinitionBubble();

                //subscribe to bubble events 
                _currentBubble.BubbleClosed += OnBubbleClosed;
                _currentBubble.AddTermRequested += OnAddTermRequested;
                _currentBubble.EditTermRequested += OnEditTermRequested;
                _currentBubble.DeleteTermRequested += OnDeleteTermRequested;
                _currentBubble.PromoteTermRequested += OnPromoteTermRequested;
                _currentBubble.ViewFilterChanged += OnViewFilterChanged;

                string currentProjectName = _currentProject?.ProjectCode ?? "Unknown Project"; 
                List <UserProjectInfo> availableProjects = GetAvailableProjects(); 

                _currentBubble.ShowDefinition(searchTerm, definitions, currentProjectName, availableProjects); 

                Console.WriteLine("Definition bubble displayed"); 
            }catch (Exception ex)
            {
                Console.WriteLine($"Error showing bubble: {ex.Message}"); 
            }
        }

        private void ShowNoTextSelectedBubble()
        {
            try
            {
                var emptyList = new List<AbbreviationData>();

                ShowDefinitionBubble("No text selected", emptyList); 
            }catch (Exception ex)
            {
                Logger.Log($"Error showing no-text bubble: {ex.Message}"); 
            }
        }

        private void ShowErrorBubble(string errorMessage)
        {
            try
            {
                var emptyList = new List<AbbreviationData>();
                ShowDefinitionBubble(errorMessage, emptyList); 
            }catch(Exception ex) 
            {
                Logger.Log($"Error showing error bubble: {ex.Message}"); 
            }
        }

        private void CloseBubbleIfOpen()
        {
            try
            {
                if(_currentBubble != null)
                {
                    _currentBubble.BubbleClosed -= OnBubbleClosed;
                    _currentBubble.AddTermRequested -= OnAddTermRequested;
                    _currentBubble.EditTermRequested -= OnEditTermRequested;
                    _currentBubble.DeleteTermRequested -= OnDeleteTermRequested;
                    _currentBubble.PromoteTermRequested -= OnPromoteTermRequested; 
                    _currentBubble.ViewFilterChanged -= OnViewFilterChanged;
                    _currentBubble.CloseBubble();
                    _currentBubble = null; 
                }
            }catch (Exception ex)
            {
                Logger.Log($"error closing bubble: {ex.Message}"); 
            }
        }

        #endregion

        #region Project Management 

        

        /// <summary>
        ///gets the list of all projects the user has access to
        /// </summary>
        public List<UserProjectInfo> GetAvailableProjects()
        {
            return new List<UserProjectInfo>(_userProjects);
        }

        /// <summary>
        ///gets the currently active project
        /// </summary>
        public UserProjectInfo? GetCurrentProject()
        {
            return _currentProject;
        }

        #endregion


        #region Event Handlers 
        private void OnBubbleClosed(object sender, EventArgs e)
        {
            try
            {
                Logger.Log("Bubble closed by user"); 

                if(_currentBubble != null)
                {
                    _currentBubble.BubbleClosed -= OnBubbleClosed;
                    _currentBubble.AddTermRequested -= OnAddTermRequested;
                    _currentBubble.EditTermRequested -= OnEditTermRequested; 
                    _currentBubble.DeleteTermRequested -= OnDeleteTermRequested; 
                    _currentBubble.PromoteTermRequested -= OnPromoteTermRequested; 
                    _currentBubble.ViewFilterChanged -= OnViewFilterChanged;

                    _currentBubble = null; 
                }
            }catch (Exception ex)
            {
                Logger.Log($"Error handling bubble close: {ex.Message}"); 
            }
        }

        private void OnAddTermRequested(object sender, AddTermEventArgs e)
        {
            try
            {
                Logger.Log($"Add term requested for '{e.SearchTerm}'");

                // Check if user can add to project
                bool canAddToProject = false;
                if (_databaseHandler != null && _permissionService != null)
                {
                    int userId = _databaseHandler.CurrentUserId;
                    int projectId = _databaseHandler.CurrentProjectId;
                    canAddToProject = _permissionService.CanAddTermsDirectly(userId, projectId);
                }

                var addTermWindow = new AddTermWindow(e.SearchTerm, canAddToProject, _userProjects, _currentProject);

                //subscribe to the term added event
                addTermWindow.TermAdded += OnTermAdded;

                //show the window as a dialog 
                bool? result = addTermWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error opening Add Term Window: {ex.Message}"); 
            }


        }

        private void OnTermAdded(object? sender, TermAddedEventArgs e)
        {
            try
            {
                Logger.Log($"Saving term '{e.Abbreviation}' to {e.TargetDatabase} database");

                if (_databaseHandler != null && _permissionService != null && _personalDatabaseService != null)
                {
                    int userId = _databaseHandler.CurrentUserId;
                    //int projectId = _databaseHandler.CurrentProjectId;

                    if (e.TargetDatabase == "Personal")
                    {
                        //user chose Personal database
                        bool success = _personalDatabaseService.AddPersonalAbbreviation(
                            userId,
                            e.Abbreviation,
                            e.Definition,
                            e.Category,
                            e.Notes);

                        if (success)
                        {
                            Logger.Log($"Term '{e.Abbreviation}' added to PERSONAL database");
                            MessageBox.Show(
                                $"Term '{e.Abbreviation}' added to your personal database!",
                                "Success",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            Logger.Log($"Failed to save term to personal database");
                            MessageBox.Show(
                                "Failed to save term. Term may already exist in your personal database.",
                                "Save Failed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                    else if (e.TargetDatabase == "Project")
                    {

                        if (!e.TargetProjectId.HasValue)
                        {
                            Logger.Log("No project selected for adding term");
                            MessageBox.Show(
                                "Please select a project to add the term to.",
                                "No Project Selected",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            return;
                        }

                        int selectedProjectId = e.TargetProjectId.Value; 

                        //user chose Project database - check permissions first
                        bool canAddDirectly = _permissionService.CanAddTermsDirectly(userId, selectedProjectId);

                        if (canAddDirectly)
                        {
                            int originalProjectId = _databaseHandler.CurrentProjectId; 
                            _databaseHandler.SetUserContext(userId, selectedProjectId); 
                            // User has permission - add directly to project
                            bool success = _databaseHandler.AddAbbreviation(
                                e.Abbreviation,
                                e.Definition,
                                e.Category,
                                e.Notes,
                                "User");

                            _databaseHandler.SetUserContext(userId, originalProjectId); 

                            if (success)
                            {
                                var selectedProject = _userProjects.FirstOrDefault(p => p.ProjectID == selectedProjectId);
                                string projectName = selectedProject?.DisplayName ?? $"Project {selectedProjectId}";

                                Logger.Log($"Term '{e.Abbreviation}' added to PROJECT database: {projectName}");
                                MessageBox.Show(
                                    $"Term '{e.Abbreviation}' added successfully to project database!",
                                    "Success",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                            else
                            {
                                Logger.Log($"Failed to save term to project database");
                                MessageBox.Show(
                                    "Failed to save term. Term may already exist in project database.",
                                    "Save Failed",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            // User doesn't have permission, this shouldn't happen if UI is configured correctly
                            Logger.Log($"User tried to add to project without permission");
                            MessageBox.Show(
                                "You don't have permission to add terms directly to the project database.",
                                "Permission Denied",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                } else
                {
                    Logger.Log("Required services not available");
                    MessageBox.Show(
                        "Database connection is not available",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error saving term: {ex.Message}");
                MessageBox.Show(
                    $"Error saving term: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles edit term request from definition bubble
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnEditTermRequested(object? sender, DefinitionBubble.EditTermEventArgs e)
        {
            try
            {
                Logger.Log($"Edit requested for '{e.Term.Abbreviation}'");

                if (_databaseHandler == null || _permissionService == null || _personalDatabaseService == null)
                {
                    Logger.Log("Required services not available");
                    MessageBox.Show(
                        "Service not available",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                int userId = _databaseHandler.CurrentUserId;
                int projectId = _databaseHandler.CurrentProjectId;

                // Check if this is a personal term
                var personalTerm = _personalDatabaseService.FindPersonalAbbreviation(e.Term.Abbreviation, userId);
                bool isPersonalTerm = personalTerm != null;

                // Check permissions for project terms
                bool canEditProject = _permissionService.CanEditTermsDirectly(userId, projectId);

                // Determine if user can edit this term
                bool canEdit = isPersonalTerm || canEditProject;

                if (!canEdit)
                {
                    Logger.Log($"User does not have permission to edit '{e.Term.Abbreviation}'");
                    MessageBox.Show(
                        $"You don't have permission to edit project terms.\n\n" +
                        $"You can only edit terms in your personal database.",
                        "Permission Denied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Show edit window
                var editWindow = new EditTermWindow(e.Term, isPersonalTerm);
                editWindow.TermEdited += OnTermEdited;

                bool? result = editWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling edit request: {ex.Message}");
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the actual term editing after user confirms changes
        /// </summary>
        private void OnTermEdited(object? sender, EditTermWindow.TermEditedEventArgs e)
        {
            try
            {
                Logger.Log($"Saving edits for '{e.Abbreviation}'");

                if (_databaseHandler == null || _personalDatabaseService == null)
                {
                    Logger.Log("Required services not available");
                    return;
                }

                int userId = _databaseHandler.CurrentUserId;
                bool success;

                if (e.IsPersonalTerm)
                {
                    // Update personal database
                    success = _personalDatabaseService.UpdatePersonalAbbreviation(
                        userId,
                        e.Abbreviation,
                        e.NewDefinition,
                        e.NewCategory,
                        e.NewNotes);

                    if (success)
                    {
                        Logger.Log($"Updated personal term '{e.Abbreviation}'");
                        MessageBox.Show(
                            $"Personal term '{e.Abbreviation}' updated successfully!",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        Logger.Log($"Failed to update personal term '{e.Abbreviation}'");
                        MessageBox.Show(
                            "Failed to update personal term.",
                            "Update Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                else
                {
                    // Update project database
                    success = _databaseHandler.UpdateAbbreviation(
                        e.Abbreviation,
                        e.NewDefinition,
                        e.NewCategory,
                        e.NewNotes,
                        userId,
                        "User edit");

                    if (success)
                    {
                        Logger.Log($"Updated project term '{e.Abbreviation}'");
                        MessageBox.Show(
                            $"Project term '{e.Abbreviation}' updated successfully!",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        Logger.Log($"Failed to update project term '{e.Abbreviation}'");
                        MessageBox.Show(
                            "Failed to update project term.",
                            "Update Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }

                // Close the bubble to refresh
                CloseBubbleIfOpen();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error saving term edits: {ex.Message}");
                MessageBox.Show(
                    $"Error saving changes: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles delete term request from the definition bubble 
        /// Checks permissions and deleted appropriate database 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDeleteTermRequested(object? sender, DefinitionBubble.DeleteTermEventArgs e)
        {
            try
            {
                Logger.Log($"Delete requested for '{e.Term.Abbreviation}'");

                if (_databaseHandler == null || _permissionService == null || _personalDatabaseService == null)
                {
                    Logger.Log("Required services not available");
                    MessageBox.Show(
                        "Service not available",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                int userId = _databaseHandler.CurrentUserId;
                int projectId = _databaseHandler.CurrentProjectId;

                // Check if this is a personal term
                var personalTerm = _personalDatabaseService.FindPersonalAbbreviation(e.Term.Abbreviation, userId);
                bool isPersonalTerm = personalTerm != null;

                // Check permissions for project terms
                bool canDeleteProject = _permissionService.CanDeleteTermsDirectly(userId, projectId);

                // Determine if user can delete this term
                bool canDelete = isPersonalTerm || canDeleteProject;

                if (!canDelete)
                {
                    Logger.Log($"User does not have permission to delete '{e.Term.Abbreviation}'");
                    MessageBox.Show(
                        $"You don't have permission to delete project terms.\n\n" +
                        $"You can only delete terms in your personal database.",
                        "Permission Denied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Perform deletion
                bool success;

                if (isPersonalTerm)
                {
                    // Delete from personal database
                    success = _personalDatabaseService.DeletePersonalAbbreviation(userId, e.Term.Abbreviation);

                    if (success)
                    {
                        Logger.Log($"Deleted personal term '{e.Term.Abbreviation}'");
                        MessageBox.Show(
                            $"Personal term '{e.Term.Abbreviation}' deleted successfully!",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Close bubble
                        CloseBubbleIfOpen();
                    }
                    else
                    {
                        Logger.Log($"Failed to delete personal term '{e.Term.Abbreviation}'");
                        MessageBox.Show(
                            "Failed to delete personal term.",
                            "Delete Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                else
                {
                    // Delete from project database
                    success = _databaseHandler.DeleteAbbreviation(
                        e.Term.Abbreviation,
                        userId,
                        "User deleted term");

                    if (success)
                    {
                        Logger.Log($"Deleted project term '{e.Term.Abbreviation}'");
                        MessageBox.Show(
                            $"Project term '{e.Term.Abbreviation}' deleted successfully!",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Close bubble
                        CloseBubbleIfOpen();
                    }
                    else
                    {
                        Logger.Log($"Failed to delete project term '{e.Term.Abbreviation}'");
                        MessageBox.Show(
                            "Failed to delete project term.",
                            "Delete Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling delete request: {ex.Message}");
                MessageBox.Show(
                    $"Error deleting term: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// Handles promote term request, shows promotion dialog
        /// </summary>
        private void OnPromoteTermRequested(object? sender, DefinitionBubble.PromoteTermEventArgs e)
        {
            try
            {
                Logger.Log($"Promote requested for '{e.Term.Abbreviation}'");

                if (_databaseHandler == null || _permissionService == null)
                {
                    Logger.Log("Required services not available");
                    MessageBox.Show(
                        "Service not available",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Check if term is from Personal database
                if (e.Term.Source != "Personal")
                {
                    Logger.Log($"Cannot promote - not a personal term (Source: {e.Term.Source})");
                    MessageBox.Show(
                        "Only terms from your Personal database can be promoted.",
                        "Cannot Promote",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                int userId = _databaseHandler.CurrentUserId;
                int projectId = _databaseHandler.CurrentProjectId;

                // Check if user can add to project directly
                bool canAddDirectly = _permissionService.CanAddTermsDirectly(userId, projectId);

                // Show promotion window
                var promoteWindow = new PromoteTermWindow(e.Term, canAddDirectly);
                promoteWindow.TermPromoted += OnTermPromoted;

                bool? result = promoteWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling promote request: {ex.Message}");
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the actual promotion after user confirms
        /// </summary>
        private void OnTermPromoted(object? sender, PromoteTermWindow.TermPromotedEventArgs e)
        {
            try
            {
                Logger.Log($"Processing promotion for '{e.Abbreviation}'");

                if (_databaseHandler == null || _personalDatabaseService == null || _permissionService == null)
                {
                    Logger.Log("Required services not available");
                    return;
                }

                int userId = _databaseHandler.CurrentUserId;
                int projectId = _databaseHandler.CurrentProjectId;

                // Check permissions
                bool canAddDirectly = _permissionService.CanAddTermsDirectly(userId, projectId);

                if (canAddDirectly)
                {
                    // User has permission - add directly to project
                    Logger.Log($"User has permission - promoting directly");

                    // Get the full term from personal database
                    var personalTerm = _personalDatabaseService.FindPersonalAbbreviation(e.Abbreviation, userId);
                    
                    if (personalTerm == null)
                    {
                        Logger.Log($"Personal term '{e.Abbreviation}' not found");
                        MessageBox.Show(
                            "Could not find term in personal database.",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    // Add to project database
                    bool success = _databaseHandler.AddAbbreviation(
                        personalTerm.Abbreviation,
                        personalTerm.Definition,
                        personalTerm.Category,
                        personalTerm.Notes,
                        "Promoted from Personal");

                    if (success)
                    {
                        Logger.Log($"Successfully promoted '{e.Abbreviation}' to project");
                        MessageBox.Show(
                            $"Term '{e.Abbreviation}' has been promoted to the project database!",
                            "Promotion Successful",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        Logger.Log($"Failed to promote - term may already exist in project");
                        MessageBox.Show(
                            "Failed to promote term. It may already exist in the project database.",
                            "Promotion Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                else
                {
                    // User needs approval - create term request
                    Logger.Log($"User needs approval - creating term request");

                    bool success = _personalDatabaseService.RequestPromotionToProject(
                        userId,
                        projectId,
                        e.Abbreviation,
                        e.Reason);

                    if (success)
                    {
                        Logger.Log($"Successfully created promotion request for '{e.Abbreviation}'");
                        MessageBox.Show(
                            $"Promotion request created for '{e.Abbreviation}'!\n\n" +
                            "A project manager will review your request.",
                            "Request Submitted",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        Logger.Log($"Failed to create promotion request");
                        MessageBox.Show(
                            "Failed to create promotion request.",
                            "Request Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }

                // Refresh the bubble to show updated term
                CloseBubbleIfOpen();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error processing promotion: {ex.Message}");
                MessageBox.Show(
                    $"Error promoting term: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// User changes project filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnViewFilterChanged(object? sender, DefinitionBubble.ViewFilterChangedEventArgs e)
        {
            try
            {
                Logger.Log($"View filter changed to: {e.NewFilter.DisplayName}");

                if (_searchService == null || _databaseHandler == null)
                {
                    Logger.Log("Required services not available");
                    return;
                }

                int userId = _databaseHandler.CurrentUserId;

                if (_currentBubble != null)
                {
                    string currentProjectName = _currentProject?.ProjectCode ?? "Unknown Project"; 
                    List<UserProjectInfo> availableProjects = GetAvailableProjects(); 

                    _currentBubble.UpdateViewFilter(e.NewFilter); 
                    Logger.Log($"Bubble filter updated to {e.NewFilter.DisplayName}"); 
                }

                /*
                // Perform search based on filter type
                List<AbbreviationData> filteredResults;

                switch (e.NewFilter.Type)
                {
                    case FilterType.All:
                        // Search ALL projects + personal
                        Logger.Log("Searching ALL projects and personal");
                        var allResults = _searchService.SearchAllProjects(e.SearchTerm, userId, _userProjects);
                        filteredResults = SearchService.GetAllResultsAsList(allResults);
                        break;

                    case FilterType.PersonalOnly:
                        // Search only personal
                        Logger.Log("Searching Personal only");
                        var personalResults = _searchService.Search(e.SearchTerm, SearchScope.Personal, userId, 0);
                        filteredResults = SearchService.GetAllResultsAsList(personalResults);
                        break;

                    case FilterType.SpecificProject:
                        // Search specific project only
                        if (!e.NewFilter.ProjectID.HasValue)
                        {
                            Logger.Log("Project filter has no ProjectID");
                            filteredResults = new List<AbbreviationData>();
                            break;
                        }

                        Logger.Log($"Searching specific project: {e.NewFilter.DisplayName}");
                        var projectResults = _searchService.Search(
                            e.SearchTerm, 
                            SearchScope.Project, 
                            userId, 
                            e.NewFilter.ProjectID.Value);
                        filteredResults = SearchService.GetAllResultsAsList(projectResults);
                        break;

                    default:
                        Logger.Log("Unknown filter type");
                        filteredResults = new List<AbbreviationData>();
                        break;
                }

                // Update the bubble with filtered results
                if (_currentBubble != null)
                {
                    string currentProjectName = _currentProject?.ProjectCode ?? "Unknown Project";
                    List<UserProjectInfo> availableProjects = GetAvailableProjects();

                    _currentBubble.ShowDefinition(
                        e.SearchTerm, 
                        filteredResults, 
                        currentProjectName, 
                        availableProjects,
                        e.NewFilter); // PASS THE FILTER SO BUBBLE KNOWS CURRENT STATE

                    Logger.Log($"Bubble updated with {filteredResults.Count} filtered results");
                }
                */
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling view filter change: {ex.Message}");
                MessageBox.Show(
                    $"Error applying filter: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        #endregion

        #region Cleanup

        /// <summary>
        /// Clean up all services and resources
        /// </summary>
        private void CleanupServices()
        {
            try
            {
                // Stop hotkey manager
                if (_hotkeyManager != null)
                {
                    _hotkeyManager.Dispose();
                    _hotkeyManager = null;
                    Logger.Log("Hotkey manager cleaned up");
                }

                // Stop clipboard handler
                if (_clipboardHandler != null)
                {
                    _clipboardHandler.Dispose();
                    _clipboardHandler = null;
                    Logger.Log("Clipboard handler cleaned up");
                }

                // CSV parser doesn't need special cleanup
                _databaseHandler = null;
                _permissionService = null;
                _auditService = null;
                _personalDatabaseService = null;
                _searchService = null;
                Logger.Log("All services cleaned up");
            }
            catch (Exception ex)
            {
                Logger.Log($" Error during cleanup: {ex.Message}");
            }
        }

        #endregion


        #region Previous Tests
        /// <summary>
        /// TEST 1: AbbreviationData functions
        /// </summary>
        private void TestAbbreviationDataModel()
        {
            var testAbbrev = new AbbreviationData("API", "Application Programming Interface", "Software");
            MessageBox.Show($"Test Successful! {testAbbrev}", "AccronymLookupTest");

            //Test matching
            bool matches = testAbbrev.Matches("api");
            MessageBox.Show($"Matching test - 'api' matches 'API': {matches}", "Matching Test"); 
        }

        /// <summary> 
        /// TEST 2: Test the CSV parser function
        /// </summary>
        private void TestCsvParser()
        {
            Logger.Log("\n===== Testing CSV Parser =====");

            //create parser 
            var parser = new CsvParser();

            // Load the test file 
            string csvPath = "Data/test_acronyms.csv";
            bool loaded = parser.LoadFromFile(csvPath);

            if (loaded)
            {
                Logger.Log($"Successfully loaded {parser.Count} abbreviations");

                //Test some lookups
                TestLookup(parser, "API");
                TestLookup(parser, "http");
                TestLookup(parser, "xyz");
                TestLookup(parser, "ai");

                //Show summary dialog
                MessageBox.Show(
                    $"CSV Parser Test Results:\n\n" +
                    $"Loaded: {parser.Count} abbreviations\n" +
                    $"File: {parser.CurrentFilePath}\n" +
                    $"Loaded at: {parser.LastLoadTime:HH:mm:ss}\n\n" +
                    $"Search tests completed - check console for details!",
                    "CSV Parser Test",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                Logger.Log("FAILED to load CSV file");
                MessageBox.Show("Failed to load CSV file. Check console for details.",
                    "Test Failed", MessageBoxButton.OK, MessageBoxImage.Error); 
            }
        }

        /// <summary> 
        /// Helper method to test individual lookups
        /// </summary>
        /// 
        private void TestLookup(CsvParser parser, string searchTerm)
        {
            Logger.Log($"\nSearching for: '{searchTerm}'");

            var result = parser.FindAbbreviation(searchTerm);

            if (result != null)
            {
                Logger.Log($"    Found: {result.Abbreviation} = {result.Definition}");
                if (!string.IsNullOrEmpty(result.Category))
                    Logger.Log($"    Category: {result.Category}");
                if (!string.IsNullOrEmpty(result.Notes))
                    Logger.Log($"   Notes: {result.Notes}");
            }
            else
            {
                Logger.Log($"    !!! NOT FOUND !!!"); 
            }
        }

        /// <summary>
        /// Test 3: Test the hotkey manager; 
        /// </summary>
        private void TestHotkeyManager()
        {
            Logger.Log("");
            Logger.Log("===== Testing Hotkey Manager ======");

            //create hotkey manager 
            _hotkeyManager = new HotkeyManager();

            //Subscribe to hotkey events 
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

            //Try to register the hotkey 
            bool success = _hotkeyManager.RegisterHotkey();

            if (success)
            {
                MessageBox.Show(
                    "Hotkey Manager Test\n\n" +
                    " - Ctrl+Alt+L registered successfully!\n\n" +
                    "Try pressing Ctrl+Alt+L from anywhere in Windows.\n" +
                    "A message should appear in the console.\n\n" +
                    "Click OK to keep testing...",
                    "Hotkey Test",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Logger.Log(" ! Hotkey is active! Press Ctrl+Alt+L to test...");
                Logger.Log("    The application will stay running until you close it.");
            }
            else
            {
                MessageBox.Show(
                    "Hotkey Registration Failed\n\n" +
                    "Ctrl+Alt+L could not be registered.\n" +
                    "It may already be in use by another application.\n\n" +
                    "Check the console for details.",
                    "Hotkey Test Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Shutdown();
            }
        }

        #endregion


        #region Basic Components Test
        private void TestBasicComponents()
        {
            try
            {
                Logger.Log("Testing individual componsnets...");

                //Test 1: Basic clipboard access
                Logger.Log("Test 1: Basic clipboard test..."); 
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        bool hasText = Clipboard.ContainsText();
                        Logger.Log($"Clipboard basic access works: {hasText}");

                    }); 
                }
                catch (Exception ex)
                {
                    Logger.Log($"Clipboard test failed: {ex.Message}");
                    throw; 
                }

                //Test 2: Create ClipboardHandler
                Logger.Log("Test 2: Creating Clipboard Handler...");
                FailedClipboardHandler? clipboardHandler = null; 
                try
                {
                    clipboardHandler = new FailedClipboardHandler();
                    Logger.Log("ClipboardHandler created successfully"); 
                }catch (Exception ex)
                {
                    Logger.Log($"ClipboardHandler creation failed: {ex.Message}");
                    throw; 
                }

                //Test 3: Create Hotkey Manager 
                Logger.Log("Test 3: Creating HotkeyManager..."); 
                try
                {
                    _hotkeyManager = new HotkeyManager();
                    Logger.Log("Hotkey manager created successfully");
                }catch (Exception ex)
                {
                    Logger.Log($"HotkeyManager creation failed: {ex.Message}");
                    throw; 
                }

                //Test 4: Register hotkey 
                Logger.Log($"Test 4: Registering hotkey..."); 
                try
                {
                    bool success = _hotkeyManager.RegisterHotkey();
                    Logger.Log($"Hotkey registration: {(success ? "SUCCESS" : "FAILED")}"); 
                    if (!success)
                    {
                        Logger.Log("Hotkey registration failed"); 
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Hotkey registration failed {ex.Message}"); 
                }

                //Test 5: Set up event handler 
                Logger.Log("Test 5: Setting up event handler..."); 
                try
                {
                    _hotkeyManager.HotkeyPressed += (sender, e) =>
                    {
                        Logger.Log("HOTKEY EVENT FIRED");
                        MessageBox.Show("Hotkey test worked! The event system is functioning.", "Success!");
                    };
                    Logger.Log("Event handler setup complete"); 
                }catch(Exception ex)
                {
                    Logger.Log($"Event handler setup failed: {ex.Message}");
                    throw; 
                }

                Logger.Log("\n ALL TESTS PASSED WOOT WOOT");
                Logger.Log("Application is ready for testing!");
                Logger.Log("Press Ctrl+Alt+L to test hotkey detection"); 

            }catch (Exception ex)
            {
                Logger.Log($"\n Component test failed at: {ex.Message}");
                throw; 
            }
        }

        #endregion
    }
}
