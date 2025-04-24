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

// Added using statements
using System.Collections.ObjectModel; // For ObservableCollection
using Microsoft.Win32; // For File Dialogs
using SkullRMacroCLI; // For the C++/CLI wrapper
using System.Windows.Threading; // For DispatcherTimer
// Added for VK to Key conversion
using System.Runtime.InteropServices;
// Added for Popup
using System.Windows.Controls.Primitives;

namespace SkullRMacro
{
    /// <summary>
    /// Interaction logic for MacroEditor.xaml
    /// </summary>
    public partial class MacroEditor : Window
    {
        // Field to hold the macro core wrapper instance
        private ManagedMacroCore macroCoreWrapper;
        // Timer for real-time updates
        private DispatcherTimer updateTimer;

        // Collection for the event timeline data binding
        public ObservableCollection<MacroStepViewModel> MacroSteps { get; set; }

        // Internal clipboard for copy/paste
        private List<MacroStepViewModel> _copiedSteps = new List<MacroStepViewModel>();

        // Reference to the currently open delay options popup
        private Popup? _delayOptionsPopup;

        // Constructor updated to accept the wrapper instance
        public MacroEditor(ManagedMacroCore wrapper)
        {
            InitializeComponent();
            this.macroCoreWrapper = wrapper;
            MacroSteps = new ObservableCollection<MacroStepViewModel>();
            this.DataContext = this;

            // Initialize the timer
            updateTimer = new DispatcherTimer();
            updateTimer.Interval = TimeSpan.FromMilliseconds(100); // Update interval (adjust as needed)
            updateTimer.Tick += UpdateTimer_Tick;

            // Initial button states
            StopButton.IsEnabled = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Save macro changes (if editing existing or unsaved new)
            // OK might just close the window if saved, or prompt to save.
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop recording if active when cancelling
            if (!StopButton.IsEnabled) // Recording if Stop button is enabled
            {
                try { macroCoreWrapper?.StopRecording(""); } catch { /* Ignore */ }
                updateTimer.Stop();
            }
            this.DialogResult = false;
            this.Close();
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            // Show the settings dialog first
            RecordingSettingsDialog settingsDialog = new RecordingSettingsDialog();
            settingsDialog.Owner = this; // Set owner to center dialog on editor window
            bool? dialogResult = settingsDialog.ShowDialog();

            // Proceed only if the user clicked the "Recording" button in the dialog
            if (dialogResult == true)
            {
                try
                {
                    // Apply the settings from the dialog to the core
                    macroCoreWrapper?.SetRecordingSettings(
                        settingsDialog.RecordKeystrokes,
                        settingsDialog.RecordMouseClicks,
                        settingsDialog.RecordAbsoluteMovement,
                        settingsDialog.RecordRelativeMovement,
                        settingsDialog.InsertPressDuration
                    );

                    macroCoreWrapper?.StartRecording();
                    // Update UI state
                    MacroSteps.Clear(); // Clear timeline visually on new recording
                    RecordButton.IsEnabled = false;
                    StopButton.IsEnabled = true;
                    TestButton.IsEnabled = false;
                    SaveButton.IsEnabled = false; // Can't save during recording
                    // Start the timer for real-time updates
                    updateTimer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error starting recording: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    // Restore button states if start failed
                    RecordButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                    TestButton.IsEnabled = true;
                    SaveButton.IsEnabled = true; // Or false if no data yet?
                }
            }
            // else: User cancelled the settings dialog, do nothing.
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                updateTimer.Stop(); // Stop timer first
                macroCoreWrapper?.StopRecording(""); // Stop recording (path ignored)
                
                // Final update to ensure last events are shown
                LoadEventsToTimeline(); 

                 // Update UI state
                RecordButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                TestButton.IsEnabled = true;
                SaveButton.IsEnabled = MacroSteps.Any(); // Enable Save only if there are steps

            } 
            catch (Exception ex) 
            {
                MessageBox.Show($"Error stopping recording: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 // Restore button states carefully on error?
                RecordButton.IsEnabled = true;
                StopButton.IsEnabled = false; // Should be false as we attempted to stop
                TestButton.IsEnabled = true;
                SaveButton.IsEnabled = MacroSteps.Any(); 
            }
        }

        // New Save Button Handler
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!MacroSteps.Any())
            {
                MessageBox.Show("There is nothing to save.", "Save Macro", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "SkullRMacro Files (*.amc)|*.amc|All files (*.*)|*.*";
            saveFileDialog.DefaultExt = ".amc";
            saveFileDialog.Title = "Save Macro As";

            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;
                try
                {
                    bool saved = macroCoreWrapper?.SaveMacro(filePath) ?? false;
                    if (saved)
                    {
                        MessageBox.Show($"Macro saved successfully to {filePath}", "Save Macro", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                         MessageBox.Show("Failed to save macro.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving macro: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Timer Tick handler for real-time updates
        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // This runs on the UI thread, so direct update is okay
            LoadEventsToTimeline(); 
        }

        // Assuming TestButton is the Play button
        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "SkullRMacro Files (*.amc)|*.amc|All files (*.*)|*.*";
            openFileDialog.DefaultExt = ".amc";
            openFileDialog.Title = "Open Macro File to Play";
            openFileDialog.CheckFileExists = true;

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                try
                {
                    // Optional: Load events into timeline view first?
                    // macroCoreWrapper?.LoadFromFile(filePath); // Need LoadFromFile exposed in wrapper if desired
                    // LoadEventsToTimeline(); 

                    // Play the macro
                    macroCoreWrapper?.PlayMacro(filePath);
                    
                    // TODO: Update UI state (e.g., disable play/record/stop during playback?)
                    // How to know when playback finishes to re-enable? Needs events or polling.
                    // For now, we don't disable controls during playback.
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error playing macro: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Method to load events from the wrapper into the UI timeline
        private void LoadEventsToTimeline()
        {
            try
            {
                var events = macroCoreWrapper?.GetMacroEvents();
                if (events != null)
                {
                    // --- New Logic for Formatting and Consolidation ---
                    var formattedSteps = new List<MacroStepViewModel>();
                    ulong lastEventTime = 0; // Changed from uint to ulong to match ManagedMacroEvent.Time
                    uint accumulatedDelay = 0; // Keep as uint since it's used for MinDelayMs
                    string lastActionDescription = string.Empty;
                    string lastEventType = string.Empty;
                    string lastNonDelayActionDescription = string.Empty;
                    string lastNonDelayEventType = string.Empty;

                    // Use the time of the first event as the starting point if events exist
                    if (events.Any())
                    {
                        lastEventTime = events.First().Time;
                    }

                    foreach (var ev in events)
                    {
                        MacroStepViewModel? currentStep = null;
                        string currentEventType = ev.Type ?? "Unknown";

                        // 1. Calculate delay since *last processed* event time and accumulate
                        ulong currentEventTime = ev.Time;
                        if (currentEventTime > lastEventTime)
                        {
                            ulong timeDiff = currentEventTime - lastEventTime;
                            // Safely handle potential overflow when converting to uint
                            if (timeDiff <= uint.MaxValue)
                            {
                                accumulatedDelay += (uint)timeDiff;
                            }
                            else
                            {
                                // If the delay is too large, cap it at uint.MaxValue
                                accumulatedDelay = uint.MaxValue;
                                System.Diagnostics.Debug.WriteLine($"Warning: Delay value capped at {uint.MaxValue} ms");
                            }
                        }
                        lastEventTime = currentEventTime;

                        // Skip raw delay events if any
                        if (currentEventType == "Delay") continue;

                        string description;
                        if (currentEventType == "Key")
                        {
                            string keyName = "Unknown Key";
                            string keyState = "unknown state";
                            int vkCode = -1;
                            var parts = ev.Details.Split(',');
                            foreach (var part in parts)
                            {
                                var kv = part.Trim().Split('=');
                                if (kv.Length == 2)
                                {
                                    if (kv[0].Trim().Equals("VK", StringComparison.OrdinalIgnoreCase) && int.TryParse(kv[1].Trim(), out int parsedVk))
                                    {
                                        vkCode = parsedVk;
                                        keyName = VkCodeToKeyName(vkCode);
                                    }
                                    else if (kv[0].Trim().Equals("State", StringComparison.OrdinalIgnoreCase))
                                    {
                                        keyState = kv[1].Trim().ToLowerInvariant(); 
                                    }
                                }
                            }
                            string action = keyState == "down" ? "Press" : "Release";
                            description = $"{action} {keyName}";
                        }
                        else if (currentEventType == "MouseClick")
                        {
                            string button = "Unknown Button";
                            string clickState = "unknown state";
                            string xPos = "?", yPos = "?";
                            var parts = ev.Details.Split(',');
                            foreach (var part in parts)
                            {
                                var kv = part.Trim().Split('=');
                                if (kv.Length == 2)
                                {
                                    if (kv[0].Trim().Equals("Btn", StringComparison.OrdinalIgnoreCase))
                                        button = kv[1].Trim();
                                    else if (kv[0].Trim().Equals("State", StringComparison.OrdinalIgnoreCase))
                                        clickState = kv[1].Trim().ToLowerInvariant();
                                    else if (kv[0].Trim().Equals("X", StringComparison.OrdinalIgnoreCase))
                                        xPos = kv[1].Trim();
                                    else if (kv[0].Trim().Equals("Y", StringComparison.OrdinalIgnoreCase))
                                        yPos = kv[1].Trim();
                                }
                            }
                            string action = clickState == "down" ? "Click" : "Release";
                            description = $"{action} {button} Mouse";
                        }
                        else if (currentEventType == "MouseMove")
                        {
                            string xVal = "0", yVal = "0";
                            var parts = ev.Details.Split(',');
                            foreach (var part in parts)
                            {
                                var kv = part.Trim().Split('=');
                                if (kv.Length == 2)
                                {
                                    // Validate coordinate values are integers
                                    if (kv[0] == "X" && int.TryParse(kv[1], out int x))
                                    {
                                        xVal = x.ToString();
                                    }
                                    else if (kv[0] == "Y" && int.TryParse(kv[1], out int y))
                                    {
                                        yVal = y.ToString();
                                    }
                                }
                            }
                            description = $"Move cursor {xVal} {yVal} (absolute)";
                        }
                        else if (currentEventType == "Goto")
                        {
                            string lineNumStr = "?";
                            int lineNum = -1;
                            var parts = ev.Details.Split('=');
                            if (parts.Length == 2 && parts[0] == "Line" && int.TryParse(parts[1], out lineNum))
                            {
                                lineNumStr = lineNum.ToString();
                            }
                            description = $"Go to line #{lineNumStr}";
                        }
                        else
                        {
                            description = $"{currentEventType}: {ev.Details}";
                        }

                        // Create the ViewModel for the current potential action step
                        currentStep = new MacroStepViewModel
                        {
                            EventType = currentEventType,
                            ActionDescription = description
                        };

                        // 3. Consolidate and Add Delay/Action
                        if (currentStep.EventType != lastNonDelayEventType || currentStep.ActionDescription != lastNonDelayActionDescription)
                        {
                            // If different, first add the accumulated delay (if any)
                            if (accumulatedDelay > 1) // Threshold
                            {
                                formattedSteps.Add(new MacroStepViewModel {
                                    EventType = "Delay",
                                    ActionDescription = $"Delay {accumulatedDelay} ms.",
                                    MinDelayMs = accumulatedDelay
                                });
                            }
                            accumulatedDelay = 0;

                            // Then, add the new distinct action step
                            formattedSteps.Add(currentStep);
                            
                            lastNonDelayActionDescription = currentStep.ActionDescription;
                            lastNonDelayEventType = currentStep.EventType;
                        }
                    }

                    // --- Handle any final accumulated delay after the loop --- 
                    if (accumulatedDelay > 1)
                    {
                        formattedSteps.Add(new MacroStepViewModel {
                            EventType = "Delay",
                            ActionDescription = $"Delay {accumulatedDelay} ms.",
                            MinDelayMs = accumulatedDelay
                        });
                    }

                    // --- Assign step numbers --- 
                    int stepNum = 1;
                    foreach(var step in formattedSteps)
                    {
                        step.StepNumber = stepNum++;
                    }

                    // --- Update ObservableCollection efficiently --- 
                    MacroSteps.Clear();
                    foreach(var step in formattedSteps)
                    {
                        MacroSteps.Add(step);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading events to timeline: {ex.Message}");
            }
        }

        // Helper method to convert VK Code to Key Name
        private string VkCodeToKeyName(int vkCode)
        {
            try
            {
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);
                return key.ToString();
            }
            catch
            {
                return $"VK={vkCode}"; // Fallback to VK code if conversion fails
            }
        }

        // Renumbers all steps in the MacroSteps collection sequentially
        private void RenumberSteps()
        {
            int stepNum = 1;
            // Ensure MacroSteps is not null before iterating
            if (MacroSteps == null) return;

            foreach (var step in MacroSteps)
            {
                step.StepNumber = stepNum++;
            }
        }

        // --- Placeholder handlers from original file --- 
        private void AddStepButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddEditStepDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                // Insert the new step after the selected item, or at the end
                int insertIndex = eventTimeline.SelectedIndex;
                if (insertIndex < 0 || insertIndex >= MacroSteps.Count - 1)
                {
                    MacroSteps.Add(dialog.Step);
                }
                else
                {
                    MacroSteps.Insert(insertIndex + 1, dialog.Step);
                }
                RenumberSteps();
                SaveButton.IsEnabled = true; // Adding steps enables save
                eventTimeline.ScrollIntoView(dialog.Step); // Scroll to the new step
                eventTimeline.SelectedItem = dialog.Step;
            }
        }

        private void EditStepButton_Click(object sender, RoutedEventArgs e)
        {
            if (eventTimeline.SelectedItem is MacroStepViewModel selectedStep)
            {
                // Create a clone to edit, so we don't modify the original unless OK is clicked
                var stepToEditClone = selectedStep.Clone(); 

                var dialog = new AddEditStepDialog(stepToEditClone);
                dialog.Owner = this;

                if (dialog.ShowDialog() == true)
                {
                    // User clicked OK, apply changes back to the original selected step
                    // Find the original item's index
                    int originalIndex = MacroSteps.IndexOf(selectedStep);
                    if (originalIndex != -1) // Should always be found if selectedItem wasn't null
                    {
                        // Replace the original item with the edited clone
                        // Using ObservableCollection's indexer triggers UI update
                        MacroSteps[originalIndex] = dialog.Step; 
                        RenumberSteps(); // Ensure numbering is correct if needed
                        SaveButton.IsEnabled = true; // Editing enables save
                        eventTimeline.SelectedItem = MacroSteps[originalIndex]; // Re-select the edited item
                        eventTimeline.ScrollIntoView(MacroSteps[originalIndex]);
                    }
                    else
                    {
                         MessageBox.Show("Could not find the original step to update.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); 
                    }
                }
                // else: User cancelled the dialog, no changes needed
            }
            else
            {
                 MessageBox.Show("Please select a single step to edit.", "Edit Step", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteStepButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = eventTimeline.SelectedItems.Cast<MacroStepViewModel>().ToList(); // Materialize the list
            if (!selectedItems.Any())
            {
                MessageBox.Show("Please select one or more steps to delete.", "Delete Step", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in selectedItems)
            {
                MacroSteps.Remove(item);
            }
            RenumberSteps();
            SaveButton.IsEnabled = MacroSteps.Any(); // Re-evaluate if save should be enabled
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (eventTimeline.SelectedItem is MacroStepViewModel selectedItem)
            {
                int currentIndex = MacroSteps.IndexOf(selectedItem);
                if (currentIndex > 0)
                {
                    MacroSteps.Move(currentIndex, currentIndex - 1);
                    RenumberSteps();
                    eventTimeline.SelectedIndex = currentIndex - 1; // Keep the item selected
                    eventTimeline.ScrollIntoView(selectedItem);
                }
            }
            else
            {
                 MessageBox.Show("Please select a single step to move up.", "Move Up", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
             if (eventTimeline.SelectedItem is MacroStepViewModel selectedItem)
            {
                int currentIndex = MacroSteps.IndexOf(selectedItem);
                if (currentIndex >= 0 && currentIndex < MacroSteps.Count - 1)
                {
                    MacroSteps.Move(currentIndex, currentIndex + 1);
                    RenumberSteps();
                    eventTimeline.SelectedIndex = currentIndex + 1; // Keep the item selected
                    eventTimeline.ScrollIntoView(selectedItem);
                }
            }
             else
             {
                  MessageBox.Show("Please select a single step to move down.", "Move Down", MessageBoxButton.OK, MessageBoxImage.Information);
             }
        }

        // --- Placeholder handlers --- 
        // Removed Button_Click stubs for buttons handled above
        // Keeping placeholders for others
        private void UndoButton_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void RedoButton_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void CutButton_Click(object sender, RoutedEventArgs e)
        {   
             var selectedItems = eventTimeline.SelectedItems.Cast<MacroStepViewModel>().ToList(); 
            if (!selectedItems.Any())
            {
                MessageBox.Show("Please select one or more steps to cut.", "Cut Steps", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 1. Copy the selected items (store clones)
            _copiedSteps = selectedItems.Select(step => step.Clone()).ToList();

            // 2. Delete the original selected items
             foreach (var item in selectedItems)
            {
                MacroSteps.Remove(item);
            }

            RenumberSteps();
            SaveButton.IsEnabled = MacroSteps.Any(); // Re-evaluate save button
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = eventTimeline.SelectedItems.Cast<MacroStepViewModel>().ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Please select one or more steps to copy.", "Copy Steps", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _copiedSteps = selectedItems.Select(step => step.Clone()).ToList(); // Store clones
            // Optionally provide feedback: e.g., update status bar text
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_copiedSteps.Any())
            {
                MessageBox.Show("There are no steps copied to the clipboard.", "Paste Steps", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int insertIndex = eventTimeline.SelectedIndex;
            if (insertIndex < 0) // If nothing is selected, paste at the end
            {
                insertIndex = MacroSteps.Count -1; 
            }

            // Insert clones of the copied steps
            for (int i = 0; i < _copiedSteps.Count; i++)
            {
                MacroSteps.Insert(insertIndex + 1 + i, _copiedSteps[i].Clone());
            }

            RenumberSteps();
            SaveButton.IsEnabled = true; // Pasting adds content
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e) { /* TODO */ }

        // --- Right Toolbar Button Handlers ---

        private void TimeButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if popup is already open
            if (_delayOptionsPopup != null && _delayOptionsPopup.IsOpen)
            {
                _delayOptionsPopup.IsOpen = false;
                return;
            }

            // Ensure an item is selected to insert *after*
            if (eventTimeline.SelectedItem == null && MacroSteps.Any()) // Allow insert at end if list not empty
            {
                 // If nothing selected, maybe default to end? Or require selection?
                 // Let's require selection for now to be explicit.
                 MessageBox.Show("Please select a step in the timeline after which to insert the delay.", "Insert Delay", MessageBoxButton.OK, MessageBoxImage.Information);
                 return;
            } else if (!MacroSteps.Any()){
                 // Allow inserting if the list is empty
                 // The insertion logic will handle adding to an empty list.
            }

            // Create the content for the popup
            var delayOptionsControl = new DelayOptionsControl();
            delayOptionsControl.InsertRequested += DelayOptionsControl_InsertRequested; // Subscribe to the event

            // Create and configure the Popup
            _delayOptionsPopup = new Popup
            {
                Child = delayOptionsControl,
                PlacementTarget = TimeButton, // Place near the button
                Placement = PlacementMode.Right,
                StaysOpen = false, // Close when clicking outside
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade
            };

            // Close handler to clear reference
            _delayOptionsPopup.Closed += (s, args) => {
                delayOptionsControl.InsertRequested -= DelayOptionsControl_InsertRequested; // Unsubscribe
                 _delayOptionsPopup = null;
                 };

            _delayOptionsPopup.IsOpen = true;
        }

        // Handler for the custom event from DelayOptionsControl
        private void DelayOptionsControl_InsertRequested(object? sender, DelayInsertEventArgs e)
        {
            // Create the new delay step
            var newStep = new MacroStepViewModel
            {
                EventType = "Delay",
                MinDelayMs = e.MinDelay,
                MaxDelayMs = e.MaxDelay
            };

            // Format description based on whether it's random
            if (e.MaxDelay.HasValue)
            {
                newStep.ActionDescription = $"Delay from {e.MinDelay} to {e.MaxDelay.Value} ms.";
            }
            else
            {
                newStep.ActionDescription = $"Delay {e.MinDelay} ms.";
            }

            // Determine insert index
            int insertIndex = eventTimeline.SelectedIndex;
             if (insertIndex < 0) // Nothing selected (only possible if list was empty)
            {
                 insertIndex = -1; // Will insert at the beginning (index 0)
            } // else: insert after selected item

            // Insert into collection
            MacroSteps.Insert(insertIndex + 1, newStep);

            RenumberSteps();
            SaveButton.IsEnabled = true;
            eventTimeline.SelectedItem = newStep;
            eventTimeline.ScrollIntoView(newStep);

            // Close the popup
            if (_delayOptionsPopup != null)
            {
                _delayOptionsPopup.IsOpen = false;
            }
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            // Open the context menu associated with the button
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        // Shared event handler for all playback mode menu items
        private void PlaybackModeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string modeTag)
            {
                try
                {
                    if (Enum.TryParse<ManagedPlaybackMode>(modeTag, out var selectedMode))
                    {
                        macroCoreWrapper?.SetPlaybackMode(selectedMode);
                        // Optional: Update UI to reflect the new mode (e.g., change RepeatButton icon)
                        UpdateRepeatButtonIcon(selectedMode);
                        System.Diagnostics.Debug.WriteLine($"Playback mode set to: {selectedMode}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Invalid PlaybackMode Tag: {modeTag}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error setting playback mode: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Optional: Helper method to update the button icon based on mode
        private void UpdateRepeatButtonIcon(ManagedPlaybackMode mode)
        {
            string icon = "🔁"; // Default
            string tooltip = "Set Operation Mode";
            switch (mode)
            {
                case ManagedPlaybackMode.RunOnce: icon = "🚫"; tooltip = "Mode: No repetitions"; break;
                case ManagedPlaybackMode.HoldToRun: icon = "⏯️"; tooltip = "Mode: Press/Release to run"; break;
                case ManagedPlaybackMode.ToggleRun: icon = "🔄"; tooltip = "Mode: Press/Press to run"; break;
            }
            RepeatButton.Content = icon;
            RepeatButton.ToolTip = tooltip;
        }

        private void GotoLineButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Add transition to line", "Line number to jump to:", "1");
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                 if (int.TryParse(dialog.ResponseText, out int lineNum) && lineNum > 0)
                 {
                     // Create the new Goto step
                     var newStep = new MacroStepViewModel
                     {
                         EventType = "Goto",
                         ActionDescription = $"Go to line #{lineNum}",
                         TargetLineNumber = lineNum
                     };

                     // Determine insert index (after selected, or at end)
                     int insertIndex = eventTimeline.SelectedIndex;
                     if (insertIndex < 0)
                     {
                         insertIndex = MacroSteps.Count - 1; // Insert at the very end if nothing selected
                     }

                     // Insert into collection
                     MacroSteps.Insert(insertIndex + 1, newStep);

                     RenumberSteps();
                     SaveButton.IsEnabled = true;
                     eventTimeline.SelectedItem = newStep;
                     eventTimeline.ScrollIntoView(newStep);
                 }
                 else
                 {
                    MessageBox.Show("Invalid line number.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                 }
            }
        }

        private void AddLoopButton_Click(object sender, RoutedEventArgs e)
        {
             // Placeholder: Mimic the 'Add cycle' dialog
            MessageBox.Show("Placeholder: Show dialog for 'Start cycle from line N' and 'Number of repetitions'.", "Add Cycle", MessageBoxButton.OK, MessageBoxImage.Information);
            // Later: Create a custom dialog and add StartLoop/EndLoop steps
        }

        private void CommentButton_Click(object sender, RoutedEventArgs e)
        {
            if (eventTimeline.SelectedItem is MacroStepViewModel selectedStep)
            {
                string currentComment = selectedStep.Comment ?? string.Empty;
                var dialog = new InputDialog("Edit Comment", "Enter comment for selected step:", currentComment);
                dialog.Owner = this;

                if (dialog.ShowDialog() == true)
                {
                    // Update the comment property of the selected step
                    // Nullify if the input is empty/whitespace, otherwise set it
                    selectedStep.Comment = string.IsNullOrWhiteSpace(dialog.ResponseText) ? null : dialog.ResponseText.Trim();
                    SaveButton.IsEnabled = true; // Editing comment enables save
                    // No renumbering needed
                    // Refreshing the view should happen automatically due to INotifyPropertyChanged
                }
                // else: User cancelled
            }
            else
            {
                MessageBox.Show("Please select a single step to add or edit its comment.", "Edit Comment", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void LangChangeButton_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void SetVariableButton_Click(object sender, RoutedEventArgs e) { /* TODO */ }

        private void MouseMB4_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement logic specific to Macro Editor if different from MainWindow
        }

        private void DeleteMacroButton_Click(object sender, RoutedEventArgs e) {
             MessageBox.Show("Placeholder: Delete Macro functionality not implemented.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddMacroButton_Click(object sender, RoutedEventArgs e) {
             MessageBox.Show("Placeholder: Add Macro functionality not implemented.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RenameMacroButton_Click(object sender, RoutedEventArgs e) {
             MessageBox.Show("Placeholder: Rename Macro functionality not implemented.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void InfoMacroButton_Click(object sender, RoutedEventArgs e) {
             MessageBox.Show("Placeholder: Macro Info functionality not implemented.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveAsMacroButton_Click(object sender, RoutedEventArgs e) {
             MessageBox.Show("Placeholder: Save As functionality not implemented.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenMacroButton_Click(object sender, RoutedEventArgs e) {
             MessageBox.Show("Placeholder: Open Macro functionality not implemented.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OffsetDelaysMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Offset Delays", "Enter delay offset in milliseconds (+ or -):", "0");
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                if (int.TryParse(dialog.ResponseText, out int offset))
                {
                    var selectedItems = eventTimeline.SelectedItems.Cast<MacroStepViewModel>().ToList();
                    foreach (var step in selectedItems)
                    {
                        if (step.EventType == "Delay")
                        {
                            // Calculate new values as long to avoid intermediate overflow/underflow
                            long newMinDelayLong = (long)step.MinDelayMs + offset;
                            // Ensure we don't go below 0ms and cast back to uint
                            step.MinDelayMs = (uint)Math.Max(0L, newMinDelayLong);

                            if (step.MaxDelayMs.HasValue)
                            {
                                long newMaxDelayLong = (long)step.MaxDelayMs.Value + offset;
                                // Ensure we don't go below 0ms and cast back to uint?
                                step.MaxDelayMs = (uint)Math.Max(0L, newMaxDelayLong);
                            }
                            // Update description
                            if (step.MaxDelayMs.HasValue)
                            {
                                step.ActionDescription = $"Delay from {step.MinDelayMs} to {step.MaxDelayMs.Value} ms.";
                            }
                            else
                            {
                                step.ActionDescription = $"Delay {step.MinDelayMs} ms.";
                            }
                        }
                    }
                    SaveButton.IsEnabled = true;
                }
                else
                {
                    MessageBox.Show("Please enter a valid number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void OffsetCoordinatesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Offset Coordinates", "Enter X,Y offset (e.g., 10,-5):", "0,0");
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                var parts = dialog.ResponseText.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int xOffset) && int.TryParse(parts[1], out int yOffset))
                {
                    var selectedItems = eventTimeline.SelectedItems.Cast<MacroStepViewModel>().ToList();
                    foreach (var step in selectedItems)
                    {
                        if (step.EventType == "MouseMove" || step.EventType == "MouseClick")
                        {
                            // Check if ActionDescription is not null before using it
                            if (step.ActionDescription != null)
                            {
                                // Parse current coordinates from description
                                var coords = ExtractCoordinates(step.ActionDescription);
                                if (coords.HasValue)
                                {
                                    int newX = coords.Value.Item1 + xOffset;
                                    int newY = coords.Value.Item2 + yOffset;

                                    // Update description with new coordinates
                                    if (step.EventType == "MouseMove")
                                    {
                                        step.ActionDescription = $"Move cursor {newX} {newY} (absolute)";
                                    }
                                    else if (step.EventType == "MouseClick")
                                    {
                                        // Preserve the original click action and button info
                                        var actionInfo = ExtractClickInfo(step.ActionDescription);
                                        if (actionInfo != null) // Check result of ExtractClickInfo
                                        {
                                            step.ActionDescription = $"{actionInfo} at ({newX}, {newY})";
                                        }
                                    }
                                }
                            }
                        }
                    }
                    SaveButton.IsEnabled = true;
                }
                else
                {
                    MessageBox.Show("Please enter valid X,Y coordinates (e.g., 10,-5).", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void RemoveAllDelaysMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = eventTimeline.SelectedItems.Cast<MacroStepViewModel>().ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Please select steps to remove delays from.", "Remove Delays", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var delaySteps = selectedItems.Where(step => step.EventType == "Delay").ToList();
            foreach (var step in delaySteps)
            {
                MacroSteps.Remove(step);
            }

            if (delaySteps.Any())
            {
                RenumberSteps();
                SaveButton.IsEnabled = true;
            }
        }

        private void ClearAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MacroSteps.Any())
            {
                var result = MessageBox.Show("Are you sure you want to clear all steps?", "Clear All", 
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    MacroSteps.Clear();
                    SaveButton.IsEnabled = false;
                }
            }
        }

        // Helper method to extract coordinates from action description
        private (int, int)? ExtractCoordinates(string description)
        {
            try
            {
                // Handle mouse move format
                if (description.Contains("Move cursor"))
                {
                    var parts = description.Split(' ');
                    if (parts.Length >= 4)
                    {
                        int x = int.Parse(parts[2]);
                        int y = int.Parse(parts[3]);
                        return (x, y);
                    }
                }
                // Handle mouse click format
                else if (description.Contains("at ("))
                {
                    var coords = description.Split('(')[1].Split(')')[0].Split(',');
                    if (coords.Length == 2)
                    {
                        int x = int.Parse(coords[0].Trim());
                        int y = int.Parse(coords[1].Trim());
                        return (x, y);
                    }
                }
            }
            catch { }
            return null;
        }

        // Helper method to extract click action info - Changed return type to string?
        private string? ExtractClickInfo(string description)
        {
            try
            {
                // Use Split with StringSplitOptions to handle potential extra spaces
                var parts = description?.Split(new[] { " at " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts?.Length > 0)
                {
                    return parts[0];
                }
            }
            catch { }
            return null; // Return null if info cannot be extracted
        }
    }
} 