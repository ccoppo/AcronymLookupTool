using AcronymLookup.Core;
using AcronymLookup.Failed_tries;
using AcronymLookup.Models;
using AcronymLookup.Services;
using AcronymLookup.UI; 
using AcronymLookup.Utilities;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using static AcronymLookup.Services.ClipboardHandler;
using static AcronymLookup.UI.DefinitionBubble;
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
        private CsvParser? _csvParser;
        private DefinitionBubble? _currentBubble; 

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
                LoadCsvData();

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

            // 1. Initialize CSV Parser 
            _csvParser = new CsvParser();
            Logger.Log("Initialized CSV Parser"); 

            // 2. Initialize Clipboard Handler
            _clipboardHandler = new ClipboardHandler();
            Logger.Log("Initialized Clipboard Handler"); 

            //3. Initialize Hotkey Manager
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

            if (_hotkeyManager != null && _clipboardHandler != null && _csvParser != null)
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

        /// <summary>
        /// Load the CSV abbreviation data
        /// </summary>
        private void LoadCsvData()
        {
            Logger.Log("Loading CSV data...");

            if (_csvParser != null)
            {
                string csvPath = "Data/test_acronyms.csv";
                bool loaded = _csvParser.LoadFromFile(csvPath);

                if (loaded)
                {
                    Logger.Log($"Successfully loaded {_csvParser.Count} abbreviations from CSV");
                }
                else
                {
                    Logger.Log(" Failed to load CSV file - abbreviation lookup will not work");
                    MessageBox.Show("Could not load abbreviation data. Please check that the CSV file exists.",
                        "Data Loading Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                    // Look up the abbreviation
                    LookupAbbreviation(e.CapturedText);
                }
                else
                {
                    Logger.Log($"Text capture failed: {e.ErrorMessage}");

                    // Show error to user
                    ShowLookupResult(null, e.ErrorMessage ?? "Failed to capture text");
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
        /// Looks up an abbreviation in the CSV data
        /// </summary>
        private void LookupAbbreviation(string searchTerm)
        {
            try
            {
                if (_csvParser != null)
                {
                    Logger.Log($"Looking up: '{searchTerm}'");

                    var result = _csvParser.FindAbbreviation(searchTerm);

                    if (result != null)
                    {
                        Logger.Log($"Found: {result.Abbreviation} = {result.Definition}");
                        ShowLookupResult(result, null);
                    }
                    else
                    {
                        Logger.Log($" Not found: '{searchTerm}'");
                        ShowLookupResult(null, $"'{searchTerm}' not found in abbreviation database");
                    }
                }
                else
                {
                    Logger.Log("CSV parser not available");
                    ShowLookupResult(null, "Abbreviation database not loaded");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error during lookup: {ex.Message}");
                ShowLookupResult(null, $"Lookup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows the lookup result to the user
        /// TODO: Later this will show the bubble UI - for now just console + message box
        /// </summary>
        private void ShowLookupResult(AbbreviationData? result, string? errorMessage)
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
                    string displayTerm = errorMessage ?? "Lookup Failed";
                    ShowErrorBubble(displayTerm); 
                }
            
                /*
                if (result != null)
                {
                    // SUCCESS: Show the abbreviation definition
                    string message = $"{result.Abbreviation}\n\n{result.Definition}";

                    if (!string.IsNullOrEmpty(result.Category))
                        message += $"\n\n Category: {result.Category}";

                    if (!string.IsNullOrEmpty(result.Notes))
                        message += $"\n\n Notes: {result.Notes}";

                    Logger.Log($"Showing result to user: {result}");

                    MessageBox.Show(message,
                        "Abbreviation Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // ERROR: Show error message
                    Logger.Log($"Showing error to user: {errorMessage}");

                    MessageBox.Show(errorMessage ?? "Unknown error",
                        "Lookup Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                */
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

                //show the bubble with results
                _currentBubble.ShowDefinition(searchTerm, definitions);

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
                    _currentBubble.CloseBubble();
                    _currentBubble = null; 
                }
            }catch (Exception ex)
            {
                Logger.Log($"error closing bubble: {ex.Message}"); 
            }
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

                //todo: implement add term feature 
                MessageBox.Show(
                    $"Add term requessted for '{e.SearchTerm}'\n\n" +
                    "This feature will be implemented later!",
                    "Add Term",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling add term: {ex.Message}");
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
                _csvParser = null;
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
