using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace SkullRMacro
{
    public partial class DelayOptionsControl : UserControl
    {
        // Event to signal the insert action
        public event EventHandler<DelayInsertEventArgs>? InsertRequested;

        public DelayOptionsControl()
        {
            InitializeComponent();
        }

        private void InsertButton_Click(object sender, RoutedEventArgs e)
        {
            if (uint.TryParse(MinDelayTextBox.Text, out uint minDelay) && minDelay > 0)
            {
                uint? maxDelay = null;
                if (RandomCheckBox.IsChecked == true)
                {
                    if (uint.TryParse(MaxDelayTextBox.Text, out uint parsedMax) && parsedMax >= minDelay)
                    {
                        maxDelay = parsedMax;
                    }
                    else
                    {
                        MessageBox.Show("Invalid maximum delay. Must be a number greater than or equal to the minimum delay.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Raise the event with the parsed values
                InsertRequested?.Invoke(this, new DelayInsertEventArgs(minDelay, maxDelay));
            }
            else
            {
                MessageBox.Show("Invalid minimum delay. Must be a positive number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Input validation (allow only digits)
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+"); // Matches non-digit characters
            e.Handled = regex.IsMatch(e.Text);
        }
    }

    // EventArgs class to pass delay values
    public class DelayInsertEventArgs : EventArgs
    {
        public uint MinDelay { get; }
        public uint? MaxDelay { get; } // Nullable for fixed delay

        public DelayInsertEventArgs(uint minDelay, uint? maxDelay)
        {
            MinDelay = minDelay;
            MaxDelay = maxDelay;
        }
    }
} 