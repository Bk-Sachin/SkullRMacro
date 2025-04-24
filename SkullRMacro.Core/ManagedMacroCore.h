#pragma once

// Include the native header
#include "MacroCore.h"

// Need this for String marshalling
#include <msclr/marshal_cppstd.h>

// Forward declaration if needed, or include relevant C# types if passing complex objects

using namespace System;
using namespace System::Collections::Generic;
using namespace System::ComponentModel; // For INotifyPropertyChanged (optional)
using namespace System::Runtime::InteropServices;

namespace SkullRMacroCLI {

    // Expose the PlaybackMode enum to C#
    public enum class ManagedPlaybackMode {
        RunOnce,
        HoldToRun,
        ToggleRun
    };

    // Define a simple managed struct/class to hold event data for C#
    // This structure should mirror the info needed by MacroStepViewModel or the UI
    // Using 'ref class' allows potential future INotifyPropertyChanged if needed
    public ref class ManagedMacroEvent /* : INotifyPropertyChanged */ {
        private:
            String^ _type;
            String^ _details;
            unsigned long _time;
            // Add INotifyPropertyChanged implementation here if needed

        public:
            // Public properties for data binding
            property String^ Type {
                String^ get() { return _type; } 
                void set(String^ value) { _type = value; /* Raise PropertyChanged if implemented */ }
            }
            property String^ Details {
                String^ get() { return _details; }
                void set(String^ value) { _details = value; /* Raise PropertyChanged if implemented */ }
            }
            property unsigned long Time {
                 unsigned long get() { return _time; }
                 void set(unsigned long value) { _time = value; /* Raise PropertyChanged if implemented */ }
            }
            
            // Constructor
            ManagedMacroEvent(String^ type, String^ details, unsigned long time) {
                Type = type;
                Details = details;
                Time = time;
            }
    };


    // Managed class to wrap the native MacroCore
    public ref class ManagedMacroCore : IDisposable
    {
    private:
        // Pointer to the native C++ class instance
        MacroCore* nativeInstance;
        bool disposed;

        // Helper to convert std::vector<MacroEvent> to a managed list
        List<ManagedMacroEvent^>^ ConvertEvents(const std::vector<MacroEvent>& nativeEvents);

    public:
        // Constructor: Creates the native instance
        ManagedMacroCore();

        // Destructor and Finalizer for IDisposable pattern
        // These are the standard way to implement IDisposable in C++/CLI
        ~ManagedMacroCore(); // Dispose managed & unmanaged resources (called by user's Dispose() or using block)
        !ManagedMacroCore(); // Finalizer: Dispose only unmanaged resources (called by GC if Dispose wasn't called)

        // Public methods mirroring native class, callable from C#
        void StartRecording();
        void StopRecording(String^ path); // Use System::String^ for CLR strings
        void PlayMacro(String^ path);     // Use System::String^ for CLR strings
        // Added dedicated save method
        bool SaveMacro(String^ path);

        // Playback Mode Methods
        void SetPlaybackMode(ManagedPlaybackMode mode);
        ManagedPlaybackMode GetPlaybackMode();

        // Method to get events for UI display
        List<ManagedMacroEvent^>^ GetMacroEvents(); // Returns list of ref class objects

        // New method for settings
        void SetRecordingSettings(
            bool recordKeystrokes,
            bool recordMouseClicks,
            bool recordAbsoluteMovement,
            bool recordRelativeMovement,
            bool insertPressDuration
        );

        // No need for explicit virtual Dispose methods here
        // The destructor ~ManagedMacroCore() implicitly handles IDisposable::Dispose

    };
} 