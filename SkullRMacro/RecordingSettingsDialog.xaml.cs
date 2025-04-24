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

namespace SkullRMacro
{
    /// <summary>
    /// Interaction logic for RecordingSettingsDialog.xaml
    /// </summary>
    public partial class RecordingSettingsDialog : Window
    {
        // Public properties to access settings after dialog closes
        public bool RecordKeystrokes { get; private set; }
        public bool RecordMouseClicks { get; private set; }
        public bool RecordAbsoluteMovement { get; private set; }
        public bool RecordRelativeMovement { get; private set; }
        public bool InsertPressDuration { get; private set; }

        public RecordingSettingsDialog()
        {
            InitializeComponent();
            // You could load default/saved settings here if needed
            // For now, defaults are set in XAML IsChecked attributes
        }

        private void StartRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            RecordKeystrokes = RecordKeystrokesCheckBox.IsChecked == true;
            RecordMouseClicks = RecordMouseClicksCheckBox.IsChecked == true;
            // Determine movement recording based on the Disable checkbox
            if (DisableMovementCheckBox.IsChecked == true)
            {
                RecordAbsoluteMovement = false;
                RecordRelativeMovement = false;
            }
            else
            {
                // Read from RadioButtons only if movement is not disabled
                RecordAbsoluteMovement = RecordAbsoluteMovementRadio.IsChecked == true;
                RecordRelativeMovement = RecordRelativeMovementRadio.IsChecked == true;
            }
            InsertPressDuration = InsertPressDurationCheckBox.IsChecked == true;

            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
} 