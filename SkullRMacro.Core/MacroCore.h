#pragma once

#include <vector>
#include <string>
#include <thread>
#include <atomic>
#include <chrono>
#include "MacroEvent.h"

// Forward declare HWND if needed, or include appropriate header
#include <Windows.h>

class MacroCore {
public:
    // Playback Mode Enum
    enum class PlaybackMode {
        RunOnce,        // Play the macro once
        HoldToRun,      // Play repeatedly while a key is held
        ToggleRun       // Start/Stop playback with the same key press
    };

    // Recording settings structure
    struct RecordingSettings {
        bool recordKeystrokes = true;
        bool recordMouseClicks = true;
        bool recordAbsoluteMovement = true;
        bool recordRelativeMovement = false;
        bool insertPressDuration = true;
    };

    MacroCore();
    ~MacroCore();

    // UI Integration Points
    void startRecording();
    void stopRecording(const std::string& path);
    void playMacro(const std::string& path);
    bool SaveMacro(const std::string& path);
    
    // Add settings methods
    void setRecordingSettings(const RecordingSettings& settings);
    const RecordingSettings& getRecordingSettings() const;

    // Add playback mode methods
    void setPlaybackMode(PlaybackMode mode);
    PlaybackMode getPlaybackMode() const;

    // Optional: To get events for UI display (like eventTimeline)
    const std::vector<MacroEvent>& getEvents() const;

private:
    std::vector<MacroEvent> events;
    std::atomic<bool> is_recording;
    std::atomic<bool> is_playing;
    std::thread recorder_thread; // Separate thread for hooks
    std::thread player_thread;   // Separate thread for playback
    DWORD recorder_thread_id;    // Added: Store recorder thread ID
    std::chrono::high_resolution_clock::time_point recording_start_time_point; // Added: Store start time
    RecordingSettings current_settings; // Add settings member
    PlaybackMode current_playback_mode; // Add playback mode member

    // Hook handles (assuming Windows)
    HHOOK keyboard_hook;
    HHOOK mouse_hook;

    // Internal methods
    void recordLoop(); // The function run by recorder_thread
    void playLoop(std::vector<MacroEvent> events_to_play); // Function for player_thread
    bool loadFromFile(const std::string& path);
    bool loadFromFileInternal(const std::string& path, std::vector<MacroEvent>& target_events);

    // Static members/functions needed for Windows hooks
    static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);
    static LRESULT CALLBACK LowLevelMouseProc(int nCode, WPARAM wParam, LPARAM lParam);
    static MacroCore* instance; // Static pointer to the instance for hook callbacks
}; 