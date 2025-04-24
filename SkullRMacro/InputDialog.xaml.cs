using System;
using System.Windows;

namespace SkullRMacro
{
    public partial class InputDialog : Window
    {
        public string ResponseText { get; private set; } = string.Empty;

        // Optional: Add validation logic if needed (e.g., ensure numeric input)
        // public Func<string, bool> ValidationRule { get; set; }

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            this.Title = title;
            PromptTextBlock.Text = prompt;
            InputTextBox.Text = defaultValue;
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Add validation check here if ValidationRule is implemented
            /*
            if (ValidationRule != null && !ValidationRule(InputTextBox.Text))
            {
                MessageBox.Show("Invalid input.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; 
            }
            */
            ResponseText = InputTextBox.Text;
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
} 