using System.ComponentModel;

namespace SkullRMacro
{
    public class MacroStepViewModel : INotifyPropertyChanged
    {
        private int _stepNumber;
        public int StepNumber
        {
            get => _stepNumber;
            set { _stepNumber = value; OnPropertyChanged(nameof(StepNumber)); }
        }

        private string _actionDescription;
        public string ActionDescription
        {
            get => _actionDescription;
            set { _actionDescription = value; OnPropertyChanged(nameof(ActionDescription)); }
        }

        // Add other properties as needed (e.g., Delay, X, Y coordinates)

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 