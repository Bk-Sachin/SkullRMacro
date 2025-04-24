#include "pch.h" // Add this for precompiled headers

#include "ManagedMacroCore.h"
#include <stdexcept> // For catching potential native exceptions
#include <vcclr.h>   // For PtrToStringChars, if needed for debugging
#include <string> // Required for std::to_string
#include <utility> // For std::pair
#include <variant> // Added for std::visit

using namespace SkullRMacroCLI;
using namespace System::Runtime::InteropServices; // For Marshal
using namespace System::Collections::Generic;

// --- Native Helper Function --- 
// (Not part of the managed class, avoids C++/CLI lambda issues)
namespace {
    // Takes a native event, returns native strings for Type and Details
    std::pair<std::string, std::string> GetNativeEventStrings(const MacroEvent& nativeEv) {
        std::string typeStr = "Unknown";
        std::string detailsStr = "";

        // Use std::visit with a lambda here is fine (pure native context)
        std::visit([&](auto&& arg) {
            using T = std::decay_t<decltype(arg)>;
            if constexpr (std::is_same_v<T, KeyboardEvent>) {
                typeStr = "Key";
                detailsStr = "VK=" + std::to_string(arg.keyCode) + ", State=" + keyStateToString(arg.state);
            } else if constexpr (std::is_same_v<T, MouseMoveEvent>) {
                typeStr = "MouseMove";
                detailsStr = "X=" + std::to_string(arg.x) + ", Y=" + std::to_string(arg.y);
            } else if constexpr (std::is_same_v<T, MouseClickEvent>) {
                typeStr = "MouseClick";
                detailsStr = "Btn=" + mouseButtonToString(arg.button) + ", State=" + keyStateToString(arg.state) + ", X=" + std::to_string(arg.x) + ", Y=" + std::to_string(arg.y);
            } else if constexpr (std::is_same_v<T, GotoEvent>) {
                typeStr = "Goto";
                detailsStr = "Line=" + std::to_string(arg.targetLineNumber);
            }
        }, nativeEv.event_data);
        
        return { typeStr, detailsStr };
    }
} // end anonymous namespace

// Constructor
ManagedMacroCore::ManagedMacroCore()
    : nativeInstance(nullptr), disposed(false)
{
    try
    {
        // Create the native C++ object instance
        nativeInstance = new MacroCore();
    }
    catch (const std::exception& e)
    {
        // Convert native exception to managed exception
        String^ errorMessage = msclr::interop::marshal_as<String^>(e.what());
        // Clean up if partially constructed
        if (nativeInstance != nullptr) { delete nativeInstance; nativeInstance = nullptr; }
        throw gcnew System::Exception("Failed to initialize native MacroCore: " + errorMessage);
    }
    catch (...)
    {
        // Clean up if partially constructed
        if (nativeInstance != nullptr) { delete nativeInstance; nativeInstance = nullptr; }
        // Catch-all for other potential native errors during construction
        throw gcnew System::Exception("An unknown native error occurred during MacroCore initialization.");
    }
}

// Destructor (called by Dispose())
ManagedMacroCore::~ManagedMacroCore()
{
    // This calls the finalizer automatically if needed, then disposes managed resources
    // We just need to ensure unmanaged resources are cleaned up here or in the finalizer
    if (!disposed)
    {
        delete nativeInstance; // Clean up native resource
        nativeInstance = nullptr;
        disposed = true;
    }
    // Call finalizer AFTER cleaning up native resources in case finalizer needs them
    // Actually, C++/CLI handles this implicitly. Explicit call not needed.
    // GC::SuppressFinalize(this); // Suppress finalize if called via Dispose
}

// Finalizer (called by GC if Dispose not called)
ManagedMacroCore::!ManagedMacroCore()
{
    // Only clean up unmanaged resources here
    if (!disposed) // Check disposed flag to prevent double deletion
    {
        delete nativeInstance;
        nativeInstance = nullptr;
        disposed = true; // Mark as disposed even if via finalizer
    }
}

// Sealed Dispose method for IDisposable interface - IMPLICITLY DEFINED by destructor
// void ManagedMacroCore::Dispose()
// {
//     // Calls ~ManagedMacroCore() and GC::SuppressFinalize(this)
// }

// --- Wrapped Methods ---

void ManagedMacroCore::StartRecording()
{
    if (disposed || nativeInstance == nullptr) throw gcnew ObjectDisposedException("ManagedMacroCore");

    try
    {
        nativeInstance->startRecording();
    }
    catch (const std::exception& e) { throw gcnew System::Exception(msclr::interop::marshal_as<String^>(e.what())); }
    catch (...) { throw gcnew System::Exception("Unknown native error in StartRecording."); }
}

void ManagedMacroCore::StopRecording(String^ path)
{
    if (disposed || nativeInstance == nullptr) throw gcnew ObjectDisposedException("ManagedMacroCore");

    try
    {
        // Convert managed String^ to std::string
        std::string nativePath = msclr::interop::marshal_as<std::string>(path);
        nativeInstance->stopRecording(nativePath);
    }
    catch (const std::exception& e) { throw gcnew System::Exception(msclr::interop::marshal_as<String^>(e.what())); }
    catch (...) { throw gcnew System::Exception("Unknown native error in StopRecording."); }
}

void ManagedMacroCore::PlayMacro(String^ path)
{
    if (disposed || nativeInstance == nullptr) throw gcnew ObjectDisposedException("ManagedMacroCore");

    try
    {
        // Convert managed String^ to std::string
        std::string nativePath = msclr::interop::marshal_as<std::string>(path);
        nativeInstance->playMacro(nativePath);
    }
    catch (const std::exception& e) { throw gcnew System::Exception(msclr::interop::marshal_as<String^>(e.what())); }
    catch (...) { throw gcnew System::Exception("Unknown native error in PlayMacro."); }
}

// Added wrapper for dedicated save
bool ManagedMacroCore::SaveMacro(String^ path)
{
    if (disposed || nativeInstance == nullptr) throw gcnew ObjectDisposedException("ManagedMacroCore");

    try
    {
        // Convert managed String^ to std::string
        std::string nativePath = msclr::interop::marshal_as<std::string>(path);
        return nativeInstance->SaveMacro(nativePath);
    }
    catch (const std::exception& e) { throw gcnew System::Exception(msclr::interop::marshal_as<String^>(e.what())); }
    catch (...) { throw gcnew System::Exception("Unknown native error in SaveMacro."); }
    return false; // Return false if exception occurred before return
}

// Helper to convert native events to managed list (Refactored)
List<ManagedMacroEvent^>^ ManagedMacroCore::ConvertEvents(const std::vector<MacroEvent>& nativeEvents)
{
    List<ManagedMacroEvent^>^ managedList = gcnew List<ManagedMacroEvent^>();

    for (const auto& nativeEv : nativeEvents)
    {
        // Call the native helper to get native strings
        std::pair<std::string, std::string> eventStrings = GetNativeEventStrings(nativeEv);

        // Convert native strings to managed strings
        String^ typeStr = msclr::interop::marshal_as<String^>(eventStrings.first);
        String^ detailsStr = msclr::interop::marshal_as<String^>(eventStrings.second);
        
        // Create the managed event object
        managedList->Add(gcnew ManagedMacroEvent(typeStr, detailsStr, nativeEv.time));
    }

    return managedList;
}

// Method to get events for UI display
List<ManagedMacroEvent^>^ ManagedMacroCore::GetMacroEvents()
{
     if (disposed || nativeInstance == nullptr) throw gcnew ObjectDisposedException("ManagedMacroCore");

    try
    {
        // Get native events (consider adding thread safety/locking in native code if necessary)
        const std::vector<MacroEvent>& nativeEvents = nativeInstance->getEvents();
        return ConvertEvents(nativeEvents);
    }
    catch (const std::exception& e) { throw gcnew System::Exception(msclr::interop::marshal_as<String^>(e.what())); }
    catch (...) { throw gcnew System::Exception("Unknown native error in GetMacroEvents."); }
}

void ManagedMacroCore::SetRecordingSettings(
    bool recordKeystrokes,
    bool recordMouseClicks,
    bool recordAbsoluteMovement,
    bool recordRelativeMovement,
    bool insertPressDuration)
{
    if (disposed) {
        throw gcnew ObjectDisposedException("ManagedMacroCore");
    }

    MacroCore::RecordingSettings settings;
    settings.recordKeystrokes = recordKeystrokes;
    settings.recordMouseClicks = recordMouseClicks;
    settings.recordAbsoluteMovement = recordAbsoluteMovement;
    settings.recordRelativeMovement = recordRelativeMovement;
    settings.insertPressDuration = insertPressDuration;

    nativeInstance->setRecordingSettings(settings);
}

// --- Helper for Playback Mode Enum Conversion ---
namespace {
    MacroCore::PlaybackMode ConvertPlaybackMode(ManagedPlaybackMode managedMode) {
        switch (managedMode) {
            case ManagedPlaybackMode::HoldToRun: return MacroCore::PlaybackMode::HoldToRun;
            case ManagedPlaybackMode::ToggleRun: return MacroCore::PlaybackMode::ToggleRun;
            case ManagedPlaybackMode::RunOnce:
            default: return MacroCore::PlaybackMode::RunOnce;
        }
    }

    ManagedPlaybackMode ConvertPlaybackMode(MacroCore::PlaybackMode nativeMode) {
        switch (nativeMode) {
            case MacroCore::PlaybackMode::HoldToRun: return ManagedPlaybackMode::HoldToRun;
            case MacroCore::PlaybackMode::ToggleRun: return ManagedPlaybackMode::ToggleRun;
            case MacroCore::PlaybackMode::RunOnce:
            default: return ManagedPlaybackMode::RunOnce;
        }
    }
} // end anonymous namespace

// --- Wrapped Playback Mode Methods ---

void ManagedMacroCore::SetPlaybackMode(ManagedPlaybackMode mode)
{
    if (disposed || nativeInstance == nullptr) throw gcnew ObjectDisposedException("ManagedMacroCore");
    try {
        nativeInstance->setPlaybackMode(ConvertPlaybackMode(mode));
    }
    catch (const std::exception& e) { throw gcnew System::Exception(msclr::interop::marshal_as<String^>(e.what())); }
    catch (...) { throw gcnew System::Exception("Unknown native error in SetPlaybackMode."); }
}

ManagedPlaybackMode ManagedMacroCore::GetPlaybackMode()
{
    if (disposed || nativeInstance == nullptr) throw gcnew ObjectDisposedException("ManagedMacroCore");
    try {
        return ConvertPlaybackMode(nativeInstance->getPlaybackMode());
    }
    catch (const std::exception& e) { throw gcnew System::Exception(msclr::interop::marshal_as<String^>(e.what())); }
    catch (...) { throw gcnew System::Exception("Unknown native error in GetPlaybackMode."); }
    return ManagedPlaybackMode::RunOnce; // Default return on error
} 