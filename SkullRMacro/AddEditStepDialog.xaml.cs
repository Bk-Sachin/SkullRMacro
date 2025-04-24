using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SkullRMacro
{
    /// <summary>
    /// Interaction logic for AddEditStepDialog.xaml
    /// </summary>
    public partial class AddEditStepDialog : Window
    {
        // Property to hold the resulting/edited step
        public MacroStepViewModel Step { get; private set; }

        public AddEditStepDialog(MacroStepViewModel? existingStep = null)
        {
            InitializeComponent();
            Step = existingStep ?? new MacroStepViewModel(); // Use existing or create new

            if (existingStep != null)
            {
                // TODO: Populate controls based on existingStep
                this.Title = "Edit Macro Step";
                PopulateControls(existingStep);
            }
            else
            {
                this.Title = "Add Macro Step";
                // Set default selection (e.g., Delay)
                StepTypeComboBox.SelectedIndex = 0;
                UpdateVisiblePanel();
            }
        }

        private void PopulateControls(MacroStepViewModel step)
        {
             // Find and select the correct ComboBox item based on EventType and ActionDescription
            string actionWord = (step.ActionDescription?.Split(' ').FirstOrDefault()) ?? string.Empty;
            ComboBoxItem? itemToSelect = null;

            foreach (ComboBoxItem item in StepTypeComboBox.Items)
            {
                string? content = item.Content?.ToString();
                string? tag = item.Tag?.ToString();

                if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(tag))
                {
                    continue; // Skip items without valid Content or Tag
                }

                if (tag == step.EventType)
                {
                    if (tag == "Delay" || tag == "MouseMove") // These don't have action words like Press/Release
                    {
                        itemToSelect = item;
                        break;
                    }
                    else if (content.StartsWith(actionWord, StringComparison.OrdinalIgnoreCase)) // Match "Key Press" with "Press"
                    {
                        itemToSelect = item;
                        break;
                    }
                }
            }
            StepTypeComboBox.SelectedItem = itemToSelect;
            UpdateVisiblePanel(); // Show the correct panel first

            // Populate specific fields based on type
            try
            {
                switch (step.EventType)
                {
                    case "Delay":
                        // Use MinDelayMs and MaxDelayMs properties
                        DelayTextBox.Text = step.MinDelayMs.ToString();
                        if (step.MaxDelayMs.HasValue)
                        {
                            RandomDelayCheckBox.IsChecked = true;
                            MaxDelayTextBox.Text = step.MaxDelayMs.Value.ToString();
                        }
                        else
                        {
                            RandomDelayCheckBox.IsChecked = false;
                        }
                        break;

                    case "Key":
                        // ActionDescription: "{Press|Release} {KeyName}"
                        var keyParts = step.ActionDescription?.Split(new[] { ' ' }, 2);
                        if (keyParts?.Length == 2)
                        {
                             KeyTextBox.Text = keyParts[1]; // KeyName
                        }
                        break;

                    case "MouseClick":
                         // ActionDescription: "{Click|Release} {ButtonName} Mouse"
                         var mcParts = step.ActionDescription?.Split(' ');
                         if (mcParts?.Length >= 3)
                         {
                             string buttonName = mcParts[1];
                             // Find and select the button in the MouseButtonComboBox
                             foreach(ComboBoxItem btnItem in MouseButtonComboBox.Items)
                             {
                                 string? itemContent = btnItem.Content?.ToString();
                                 if (itemContent != null && itemContent.Equals(buttonName, StringComparison.OrdinalIgnoreCase))
                                 {
                                     MouseButtonComboBox.SelectedItem = btnItem;
                                     break;
                                 }
                             }
                         }
                        break;

                    case "MouseMove":
                         // ActionDescription: "Move cursor {X} {Y} (absolute)"
                        var mmParts = step.ActionDescription?.Split(' ');
                         if (mmParts?.Length >= 4)
                         {
                            MouseXTextBox.Text = mmParts[2];
                            MouseYTextBox.Text = mmParts[3];
                         }
                        break;
                }
            }
            catch (Exception ex)
            {
                 MessageBox.Show($"Error parsing step details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                 // Handle potential parsing errors gracefully
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (BuildStepFromControls())
            {
                this.DialogResult = true;
            }
            // Else: BuildStepFromControls showed an error message
        }

        private bool BuildStepFromControls()
        {
            if (StepTypeComboBox.SelectedItem is ComboBoxItem selectedTypeItem)
            {
                // Assume Content and Tag are not null for valid ComboBoxItems in this context
                string typeContent = selectedTypeItem.Content!.ToString()!;
                string eventType = selectedTypeItem.Tag!.ToString()!;

                Step.EventType = eventType;

                switch (eventType)
                {
                    case "Delay":
                        if (uint.TryParse(DelayTextBox.Text, out uint minDelay) && minDelay > 0)
                        {
                            if (RandomDelayCheckBox.IsChecked == true)
                            {
                                if (uint.TryParse(MaxDelayTextBox.Text, out uint maxDelay) && maxDelay >= minDelay)
                                {
                                    Step.MinDelayMs = minDelay;
                                    Step.MaxDelayMs = maxDelay;
                                    Step.ActionDescription = $"Delay from {minDelay} to {maxDelay} ms.";
                                }
                                else
                                {
                                     MessageBox.Show("Invalid maximum delay. Must be a number greater than or equal to the minimum delay.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                                     return false;
                                }
                            }
                            else // Fixed delay
                            {
                                Step.MinDelayMs = minDelay;
                                Step.MaxDelayMs = null; // Ensure Max is null for fixed delay
                                Step.ActionDescription = $"Delay {minDelay} ms.";
                            }
                            return true;
                        }
                        else
                        {
                            MessageBox.Show("Please enter a valid positive number for delay.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }

                    case "Key":
                        string keyName = KeyTextBox.Text.Trim();
                        if (!string.IsNullOrWhiteSpace(keyName))
                        {
                             string action = typeContent.Contains("Press") ? "Press" : "Release";
                             Step.ActionDescription = $"{action} {keyName}";
                             return true;
                        }
                        else
                        {
                             MessageBox.Show("Please enter a key name.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }

                     case "MouseClick":
                        if (MouseButtonComboBox.SelectedItem is ComboBoxItem selectedButton && selectedButton.Content != null)
                        {
                            string buttonName = selectedButton.Content.ToString()!;
                             string action = typeContent.Contains("Click") ? "Click" : "Release";
                             Step.ActionDescription = $"{action} {buttonName} Mouse";
                            return true;
                        }
                        else
                        {
                            MessageBox.Show("Please select a mouse button.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }

                     case "MouseMove":
                         if (int.TryParse(MouseXTextBox.Text, out int x) && int.TryParse(MouseYTextBox.Text, out int y))
                         {
                            Step.ActionDescription = $"Move cursor {x} {y} (absolute)";
                            return true;
                         }
                         else
                         {
                             MessageBox.Show("Please enter valid integer X and Y coordinates.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                         }

                    // Add cases for other types

                    default:
                        MessageBox.Show("Selected step type is not fully implemented yet.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
                        return false;
                }
            }
            else
            {
                MessageBox.Show("Please select a step type.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void StepTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateVisiblePanel();
        }

        private void UpdateVisiblePanel()
        {
            // Hide all panels initially
            DelayPanel.Visibility = Visibility.Collapsed;
            KeyPanel.Visibility = Visibility.Collapsed;
            MouseClickPanel.Visibility = Visibility.Collapsed;
            MouseMovePanel.Visibility = Visibility.Collapsed;

            if (StepTypeComboBox.SelectedItem is ComboBoxItem selectedTypeItem)
            {
                // Assume Content and Tag are not null for valid ComboBoxItems in this context
                string typeContent = selectedTypeItem.Content!.ToString()!;
                string eventType = selectedTypeItem.Tag!.ToString()!;

                switch (eventType) // Use Tag for broader category
                {
                    case "Delay":
                        DelayPanel.Visibility = Visibility.Visible;
                        break;
                    case "Key":
                        KeyPanel.Visibility = Visibility.Visible;
                        break;
                    case "MouseClick":
                        MouseClickPanel.Visibility = Visibility.Visible;
                        break;
                    case "MouseMove":
                        MouseMovePanel.Visibility = Visibility.Visible;
                        break;
                    // Add cases for other types
                }
            }
        }

        // Input validation for numeric TextBoxes
        private static readonly Regex _numberRegex = new Regex("[^0-9.-]+"); // Allows numbers, dot, and minus sign
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {   
            // Basic check: Allow only digits for delay and coordinates
             e.Handled = !int.TryParse(e.Text, out _);
        }
    }
} 