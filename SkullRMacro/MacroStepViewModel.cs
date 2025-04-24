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

        private string? _actionDescription;
        public string? ActionDescription
        {
            get => _actionDescription;
            set { _actionDescription = value; OnPropertyChanged(nameof(ActionDescription)); }
        }

        // Add other properties as needed (e.g., Delay, X, Y coordinates)
        private string? _eventType;
        public string? EventType
        {
            get => _eventType;
            set { _eventType = value; OnPropertyChanged(nameof(EventType)); }
        }

        private string? _comment;
        public string? Comment
        {
            get => _comment;
            set { _comment = value; OnPropertyChanged(nameof(Comment)); }
        }

        // --- Delay Properties ---
        private uint _minDelayMs;
        public uint MinDelayMs // For fixed delay, this holds the value. For random, the minimum.
        {
            get => _minDelayMs;
            set { _minDelayMs = value; OnPropertyChanged(nameof(MinDelayMs)); }
        }

        private uint? _maxDelayMs; // Null if delay is fixed
        public uint? MaxDelayMs 
        {
            get => _maxDelayMs;
            set { _maxDelayMs = value; OnPropertyChanged(nameof(MaxDelayMs)); }
        }

        // --- Goto Property ---
        private int? _targetLineNumber; // Null for non-Goto steps
        public int? TargetLineNumber
        {
            get => _targetLineNumber;
            set { _targetLineNumber = value; OnPropertyChanged(nameof(TargetLineNumber)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Method to create a shallow clone for Copy/Paste
        public MacroStepViewModel Clone()
        {
            return (MacroStepViewModel)this.MemberwiseClone();
        }
    }
} 