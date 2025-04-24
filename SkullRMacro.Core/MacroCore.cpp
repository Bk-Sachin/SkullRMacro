#include "pch.h" // Add this for precompiled headers

#include "MacroCore.h"
#include <Windows.h>
#include <vector>
#include <fstream>
#include <chrono>
#include <stdexcept> // For exceptions
#include <iostream> // For basic error logging, replace with proper logging if available
#include <process.h> // For _beginthreadex if preferred over std::thread, or GetCurrentThreadId

// Placeholder for JSON library - ensure this is included in your project
// You might need to add this via vcpkg or manually
#include "json.hpp" // Changed to use quotes for local project include

using json = nlohmann::json;

// Static instance pointer initialization
MacroCore* MacroCore::instance = nullptr;

// --- Enum to String Helper Implementations ---
// (These should ideally be in a separate utility file or within MacroEvent.cpp if created)

std::string mouseButtonToString(MouseButton button) {
    switch (button) {
        case MouseButton::Left: return "left";
        case MouseButton::Right: return "right";
        case MouseButton::Middle: return "middle";
        case MouseButton::X1: return "x1";
        case MouseButton::X2: return "x2";
        default: return "unknown";
    }
}

MouseButton stringToMouseButton(const std::string& str) {
    if (str == "left") return MouseButton::Left;
    if (str == "right") return MouseButton::Right;
    if (str == "middle") return MouseButton::Middle;
    if (str == "x1") return MouseButton::X1;
    if (str == "x2") return MouseButton::X2;
    // Consider throwing an exception or returning a default/invalid value
    throw std::runtime_error("Unknown mouse button string: " + str);
}

std::string keyStateToString(KeyState state) {
    return (state == KeyState::Down) ? "down" : "up";
}

KeyState stringToKeyState(const std::string& str) {
    if (str == "down") return KeyState::Down;
    if (str == "up") return KeyState::Up;
    throw std::runtime_error("Unknown key state string: " + str);
}

// --- MacroCore Implementation ---

MacroCore::MacroCore() : 
    is_recording(false), 
    is_playing(false), 
    keyboard_hook(NULL), 
    mouse_hook(NULL),
    recorder_thread_id(0), // Initialize recorder_thread_id
    current_playback_mode(PlaybackMode::RunOnce) // Default to RunOnce
{
    if (instance != nullptr) {
        // Handle error: Singleton pattern violated or multiple instances created
        // Depending on design, this might be acceptable or require modification
        // For now, allow multiple instances, but only one sets hooks (via static instance)
        // Consider logging a warning if instance != nullptr?
        // throw std::runtime_error("Only one instance of MacroCore allowed.");
    }
    // This simple singleton pattern isn't thread-safe for lazy initialization
    // but here we assume constructor is called from main thread before hooks start
    if (instance == nullptr) {
         instance = this; // Set the static instance pointer only for the first instance
    }
}

MacroCore::~MacroCore() {
    // Ensure hooks are uninstalled and threads joined ONLY if this instance is the one managing them
    if (instance == this) {
        if (is_recording.load()) {
            stopRecording(""); // Pass empty path to avoid saving if destructor called unexpectedly
        }
         if (keyboard_hook) UnhookWindowsHookEx(keyboard_hook);
         if (mouse_hook) UnhookWindowsHookEx(mouse_hook);
         keyboard_hook = NULL;
         mouse_hook = NULL;
         instance = nullptr; // Clear the static pointer
    }

     if (is_playing.load()) {
         // Need a way to signal the player thread to stop
         is_playing.store(false); // Signal stop
         if (player_thread.joinable()) {
             player_thread.join();
         }
     }
    
    // Note: Recorder thread is joined in stopRecording
    // If destructor is called while recording without stopRecording being called first,
    // the recorder_thread might still be running. This is handled by the is_recording check above.
}

void MacroCore::startRecording() {
    // Only allow recording if this instance is the designated one
    if (instance != this) {
        throw std::runtime_error("Another MacroCore instance is already managing hooks.");
    }

    if (is_recording.load() || is_playing.load()) {
        std::cerr << "Already recording or playing." << std::endl;
        return; // Already recording or playing
    }

    events.clear(); // Clear previous events
    recording_start_time_point = std::chrono::high_resolution_clock::now(); // Capture start time
    is_recording.store(true);

    // Start the recorder thread which will set up hooks and run message loop
    recorder_thread = std::thread(&MacroCore::recordLoop, this);

    std::cout << "Recording started." << std::endl;
}

void MacroCore::stopRecording(const std::string& path) {
     // Only allow stopping if this instance is the designated one
     if (instance != this) {
         return; 
     }

    if (!is_recording.load()) {
        return; // Not recording
    }

    is_recording.store(false); // Signal the recorder thread to stop

    // Post a message to the recorder thread's message queue to unblock GetMessage/PeekMessage
    if (recorder_thread_id != 0) { 
        PostThreadMessage(recorder_thread_id, WM_NULL, 0, 0); 
    }

    // Hooks are uninstalled in the recorder_thread after the message loop exits
    if (recorder_thread.joinable()) {
        recorder_thread.join(); 
    }
    // Ensure thread ID is invalid now
    recorder_thread_id = 0;

    // REMOVED saving logic from here
    std::cout << "Recording stopped." << std::endl;
}

void MacroCore::playMacro(const std::string& path) {
     if (is_recording.load()) { // Cannot play while recording
         std::cerr << "Cannot play macro while recording is active." << std::endl;
         return;
     }
      if (is_playing.load()) { // Cannot play multiple macros simultaneously
         std::cerr << "Another macro is already playing." << std::endl;
         return;
     }

    std::vector<MacroEvent> loaded_events; // Load into a local variable
    std::vector<MacroEvent> temp_events_holder; // Use a temporary holder for loadFromFile

    // Pass the temporary holder to loadFromFile
    if (!loadFromFileInternal(path, temp_events_holder)) { 
        std::cerr << "Failed to load macro from " << path << std::endl;
        return;
    }
    loaded_events = std::move(temp_events_holder); // Move the loaded events

    if (loaded_events.empty()) {
         std::cerr << "No events to play in " << path << std::endl;
         return;
    }

    is_playing.store(true);
    // Detach player thread? Or ensure it's joined somewhere? Join in destructor for now.
    // If we allow multiple playbacks (not currently), detaching might be needed, or a manager class.
    player_thread = std::thread(&MacroCore::playLoop, this, loaded_events); // Pass loaded events by value

    std::cout << "Playback started from " << path << std::endl;
}

const std::vector<MacroEvent>& MacroCore::getEvents() const {
    // This might need locking if accessed while recording is active
    // For simplicity now, assume it's called after recording stops
    return events;
}

// --- Internal Methods ---

void MacroCore::recordLoop() {
    // This function runs in a separate thread (recorder_thread)
    // It sets up the hooks and runs a message loop to process hook events

    // Store the current thread ID so stopRecording can post messages to it
    recorder_thread_id = GetCurrentThreadId();

    // Get module handle for hook installation
    HINSTANCE hInstance = GetModuleHandle(NULL);
    if (!hInstance) {
         std::cerr << "Failed to get module handle." << std::endl;
         is_recording.store(false); // Abort recording
         recorder_thread_id = 0;
         return;
    }

    // Install low-level hooks
    // Ensure hooks are only set by the designated instance (already checked in startRecording)
    keyboard_hook = SetWindowsHookEx(WH_KEYBOARD_LL, LowLevelKeyboardProc, hInstance, 0);
    mouse_hook = SetWindowsHookEx(WH_MOUSE_LL, LowLevelMouseProc, hInstance, 0);

    if (!keyboard_hook || !mouse_hook) {
        std::cerr << "Failed to set hooks. Error code: " << GetLastError() << std::endl;
        // Cleanup partially installed hooks if necessary
        if (keyboard_hook) UnhookWindowsHookEx(keyboard_hook);
        if (mouse_hook) UnhookWindowsHookEx(mouse_hook);
        keyboard_hook = NULL;
        mouse_hook = NULL;
        is_recording.store(false); // Abort recording
        recorder_thread_id = 0;
        return;
    }

    // Required message loop for low-level hooks
    MSG msg;
    // Use PeekMessage to allow checking is_recording flag more frequently
    while (is_recording.load()) {
        // Process all pending messages first
        while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE)) {
             if (msg.message == WM_NULL && msg.hwnd == NULL && msg.wParam == 0 && msg.lParam == 0) { 
                 // Check for the specific WM_NULL posted by stopRecording
                 std::cout << "Record loop received stop signal." << std::endl;
                 goto end_loop; // Exit outer loop cleanly
             }
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
        // If no messages, sleep briefly to avoid pegging CPU
         Sleep(1); 
    }

end_loop:
    // Cleanup hooks
    if (keyboard_hook) UnhookWindowsHookEx(keyboard_hook);
    if (mouse_hook) UnhookWindowsHookEx(mouse_hook);
    keyboard_hook = NULL;
    mouse_hook = NULL;
    
    // Note: recorder_thread_id is cleared in stopRecording after join
    std::cout << "Recorder thread finished." << std::endl;
}


void MacroCore::playLoop(std::vector<MacroEvent> events_to_play) {
    // This function runs in a separate thread (player_thread)
    // It simulates the recorded input events

    if (events_to_play.empty()) {
        is_playing.store(false);
        return;
    }

    // --- Playback Mode Logic --- 
    bool should_loop = false;
    bool toggle_state = false; // For ToggleRun mode
    const int STOP_KEY = VK_F12; // Use F12 as the hardcoded stop key
    bool stop_key_was_pressed = (GetAsyncKeyState(STOP_KEY) & 0x8000) != 0;

    INPUT input[2]; // Max 2 needed (e.g., mouse move + potential click flag reset)

    for (size_t i = 0; i < events_to_play.size(); /* No increment here */) {
        const auto& ev = events_to_play[i]; // Get current event
        bool jumped = false; // Flag to indicate if we jumped

        // --- Check Stop Conditions based on Mode --- 
        if (!is_playing.load()) { 
            std::cout << "Playback stopped externally." << std::endl;
            goto end_playback; 
        }

        bool stop_key_is_pressed = (GetAsyncKeyState(STOP_KEY) & 0x8000) != 0;

        if (current_playback_mode == PlaybackMode::HoldToRun) {
            if (!stop_key_is_pressed) {
                 std::cout << "Stop key (F12) released for HoldToRun mode." << std::endl;
                 is_playing.store(false);
                 goto end_playback;
            }
        } else if (current_playback_mode == PlaybackMode::ToggleRun) {
            // Detect a fresh press of the stop key (was up, now down)
            if (stop_key_is_pressed && !stop_key_was_pressed) {
                 std::cout << "Stop key (F12) pressed for ToggleRun mode." << std::endl;
                 is_playing.store(false);
                 goto end_playback;
            }
        }
        stop_key_was_pressed = stop_key_is_pressed; // Update state for next check

        // Calculate time elapsed since playback started for this loop iteration
        auto playback_start_time = std::chrono::high_resolution_clock::now();
        auto elapsed_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::high_resolution_clock::now() - playback_start_time).count();

        // Calculate delay needed before this event based on recorded timestamps
        long long required_delay_ms = (long long)ev.time - elapsed_ms;

        // --- Precise Wait with Stop Check --- 
        if (required_delay_ms > 0) {
            auto wait_until = std::chrono::high_resolution_clock::now() + std::chrono::milliseconds(required_delay_ms);
            while (std::chrono::high_resolution_clock::now() < wait_until) {
                // Check stop conditions frequently during wait
                if (!is_playing.load()) { 
                     std::cout << "Playback stopped externally during wait." << std::endl;
                     goto end_playback;
                }
                 stop_key_is_pressed = (GetAsyncKeyState(STOP_KEY) & 0x8000) != 0;
                 if (current_playback_mode == PlaybackMode::HoldToRun && !stop_key_is_pressed) {
                      std::cout << "Stop key (F12) released during wait." << std::endl;
                      is_playing.store(false);
                      goto end_playback;
                 }
                 if (current_playback_mode == PlaybackMode::ToggleRun && stop_key_is_pressed && !stop_key_was_pressed) {
                      std::cout << "Stop key (F12) pressed during wait." << std::endl;
                      is_playing.store(false);
                      goto end_playback;
                 }
                 stop_key_was_pressed = stop_key_is_pressed;

                // Sleep briefly to avoid busy-waiting
                std::this_thread::sleep_for(std::chrono::milliseconds(1)); 
            }
        }
        // --- End Precise Wait --- 

        // Check again after wait if playback was cancelled
        if (!is_playing.load()) {
             std::cout << "Playback cancelled after wait." << std::endl;
             goto end_playback; // Use goto to exit nested loops cleanly
        }

        // --- Prepare INPUT structure(s) --- 
        memset(input, 0, sizeof(input));
        int input_count = 0;

        // Use std::visit to handle the different event types in the variant
        std::visit([&](auto&& arg) {
            using T = std::decay_t<decltype(arg)>;
            if constexpr (std::is_same_v<T, KeyboardEvent>) {
                // Keyboard event
                input[0].type = INPUT_KEYBOARD;
                input[0].ki.wVk = arg.keyCode;
                input[0].ki.dwFlags = (arg.state == KeyState::Up) ? KEYEVENTF_KEYUP : 0;
                // Add extended key flag if necessary (e.g., for numpad keys, arrow keys)
                // Compare against known extended keys
                switch (arg.keyCode) {
                    case VK_RCONTROL:
                    case VK_RMENU: // AltGr
                    case VK_NUMLOCK: 
                    case VK_SNAPSHOT: // PrintScreen
                    case VK_CANCEL: // Pause/Break
                    case VK_INSERT:
                    case VK_DELETE:
                    case VK_HOME:
                    case VK_END:
                    case VK_PRIOR: // PageUp
                    case VK_NEXT: // PageDown
                    case VK_LEFT:
                    case VK_UP:
                    case VK_RIGHT:
                    case VK_DOWN:
                    case VK_LWIN:
                    case VK_RWIN:
                    case VK_APPS:
                         input[0].ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
                         break;
                }
                // Consider KEYEVENTF_SCANCODE if needed for specific apps/games

                input_count = 1;
            } else if constexpr (std::is_same_v<T, MouseMoveEvent>) {
                // Mouse move event
                 input[0].type = INPUT_MOUSE;
                 // Normalize coordinates to 0-65535 range
                 input[0].mi.dx = static_cast<LONG>((static_cast<double>(arg.x) * 65535.0) / GetSystemMetrics(SM_CXSCREEN));
                 input[0].mi.dy = static_cast<LONG>((static_cast<double>(arg.y) * 65535.0) / GetSystemMetrics(SM_CYSCREEN));
                 input[0].mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE | MOUSEEVENTF_VIRTUALDESK;
                 input_count = 1;

            } else if constexpr (std::is_same_v<T, MouseClickEvent>) {
                // Mouse click event - Requires Move then Click/Release
                // Ensure mouse is at the correct position before click
                input[0].type = INPUT_MOUSE;
                input[0].mi.dx = static_cast<LONG>((static_cast<double>(arg.x) * 65535.0) / GetSystemMetrics(SM_CXSCREEN));
                input[0].mi.dy = static_cast<LONG>((static_cast<double>(arg.y) * 65535.0) / GetSystemMetrics(SM_CYSCREEN));
                input[0].mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE | MOUSEEVENTF_VIRTUALDESK;

                // Set up the second input structure for the click/release
                input[1].type = INPUT_MOUSE;
                // dwFlags determine the action (down/up) and button
                switch (arg.button) {
                    case MouseButton::Left:
                        input[1].mi.dwFlags = (arg.state == KeyState::Down) ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
                        break;
                    case MouseButton::Right:
                        input[1].mi.dwFlags = (arg.state == KeyState::Down) ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
                        break;
                    case MouseButton::Middle:
                        input[1].mi.dwFlags = (arg.state == KeyState::Down) ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP;
                        break;
                    case MouseButton::X1:
                         input[1].mi.dwFlags = (arg.state == KeyState::Down) ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP;
                         input[1].mi.mouseData = XBUTTON1;
                         break;
                    case MouseButton::X2:
                         input[1].mi.dwFlags = (arg.state == KeyState::Down) ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP;
                         input[1].mi.mouseData = XBUTTON2;
                         break;
                }
                 input[1].mi.dwFlags |= MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK;
                 // Position info in input[1] is ignored by system when click flags are set,
                 // but setting dx/dy is harmless.
                 input[1].mi.dx = input[0].mi.dx; 
                 input[1].mi.dy = input[0].mi.dy;
                 
                 input_count = 2; // Send move and click/release together
            } else if constexpr (std::is_same_v<T, GotoEvent>) {
                // Handle Goto Event
                int targetLine = arg.targetLineNumber;
                // Validate target line (1-based index from UI needs conversion to 0-based vector index)
                if (targetLine >= 1 && (size_t)targetLine <= events_to_play.size()) {
                    i = (size_t)targetLine - 1; // Set next loop index (will be incremented to targetLine at end)
                    jumped = true;
                    std::cout << "Jumping to line " << targetLine << std::endl;
                } else {
                    std::cerr << "Invalid Goto target line: " << targetLine << ". Stopping playback." << std::endl;
                    is_playing.store(false); // Stop playback on invalid jump
                    // No need for goto end_playback here, loop condition will handle it
                }
            }
        }, ev.event_data);

        // Stop if invalid jump occurred
        if (!is_playing.load() && !jumped) { // Ensure we only break if stopped *without* jumping
             goto end_playback; // Or just break if you prefer
        }

        // Send the input(s)
        if (input_count > 0) {
            UINT sent = SendInput(input_count, input, sizeof(INPUT));
            if (sent != input_count) {
                std::cerr << "SendInput failed or sent partial input. Error: " << GetLastError() << std::endl;
                // Stop playback on error?
                is_playing.store(false);
                goto end_playback;
            }
        }

        // Increment loop counter only if we didn't jump
        if (!jumped) {
            ++i;
        }

        // Loop determination remains the same
        should_loop = (current_playback_mode == PlaybackMode::HoldToRun || current_playback_mode == PlaybackMode::ToggleRun);
        
        // For HoldToRun, check if the key is still pressed AFTER the loop completes
        if (current_playback_mode == PlaybackMode::HoldToRun && !(GetAsyncKeyState(STOP_KEY) & 0x8000)) {
            should_loop = false;
        }

    } while (should_loop && is_playing.load()); // End do-while loop

end_playback:
    is_playing.store(false); // Mark playback as finished/stopped
    std::cout << "Playback finished/stopped." << std::endl;
}

// Helper function for loading to avoid modifying 'events' member directly during load
bool MacroCore::loadFromFileInternal(const std::string& path, std::vector<MacroEvent>& target_events) {
    target_events.clear();
    try {
        std::ifstream file(path);
        if (!file.is_open()) {
             std::cerr << "Error opening file for reading: " << path << std::endl;
            return false;
        }

        json j;
        file >> j;
        file.close();

        // Basic validation
        if (!j.contains("version") || !j.contains("events") || !j["events"].is_array()) {
            std::cerr << "Invalid macro file format: " << path << std::endl;
            return false;
        }

        // Check version (optional, but good practice)
        if (j["version"] != 1) {
             std::cerr << "Unsupported macro version: " << j["version"].get<int>() << std::endl;
             return false;
        }


        for (const auto& item : j["events"]) {
            MacroEvent ev;
            if (!item.contains("time") || !item["time"].is_number_unsigned()) {
                 std::cerr << "Invalid event format: missing or invalid time" << std::endl;
                 continue; // Skip invalid event
            }
             ev.time = item["time"].get<unsigned long>();


            if (!item.contains("type") || !item["type"].is_string()) {
                 std::cerr << "Invalid event format: missing or invalid type" << std::endl;
                continue;
            }
            std::string type = item["type"].get<std::string>();


            try {
                 if (type == "key") {
                     if (!item.contains("keyCode") || !item["keyCode"].is_number_integer() ||
                         !item.contains("state") || !item["state"].is_string()) {
                         std::cerr << "Invalid key event format." << std::endl; continue;
                     }
                     KeyboardEvent ke;
                     ke.keyCode = item["keyCode"].get<int>();
                     ke.state = stringToKeyState(item["state"].get<std::string>());
                     ev.event_data = ke;
                 } else if (type == "mouseMove") {
                     if (!item.contains("x") || !item["x"].is_number_integer() ||
                         !item.contains("y") || !item["y"].is_number_integer()) {
                          std::cerr << "Invalid mouseMove event format." << std::endl; continue;
                     }
                     MouseMoveEvent mme;
                     mme.x = item["x"].get<int>();
                     mme.y = item["y"].get<int>();
                     ev.event_data = mme;
                 } else if (type == "mouseClick") {
                     if (!item.contains("button") || !item["button"].is_string() ||
                         !item.contains("state") || !item["state"].is_string() ||
                         !item.contains("x") || !item["x"].is_number_integer() || // Load click position
                         !item.contains("y") || !item["y"].is_number_integer()) {
                          std::cerr << "Invalid mouseClick event format." << std::endl; continue;
                     }
                     MouseClickEvent mce;
                     mce.button = stringToMouseButton(item["button"].get<std::string>());
                     mce.state = stringToKeyState(item["state"].get<std::string>());
                     mce.x = item["x"].get<int>();
                     mce.y = item["y"].get<int>();
                     ev.event_data = mce;
                 } else if (type == "goto") {
                     if (!item.contains("targetLine") || !item["targetLine"].is_number_integer()) {
                          std::cerr << "Invalid goto event format." << std::endl; continue;
                     }
                     GotoEvent ge;
                     ge.targetLineNumber = item["targetLine"].get<int>();
                     ev.event_data = ge;
                 } else {
                      std::cerr << "Unknown event type: " << type << std::endl;
                     continue; // Skip unknown event type
                 }
                 target_events.push_back(ev); // Add successfully parsed event to the target vector
            } catch (const std::exception& e) {
                 std::cerr << "Error parsing event data: " << e.what() << std::endl;
                 // Continue to next event
            }
        }
        return true;

    } catch (const json::parse_error& e) {
        std::cerr << "JSON parsing error: " << e.what() << std::endl;
        return false;
    } catch (const std::exception& e) {
        std::cerr << "Error reading or processing file: " << e.what() << std::endl;
        return false;
    }
}

// Public load function now uses the internal helper and loads into the member variable
 bool MacroCore::loadFromFile(const std::string& path) {
     if (!loadFromFileInternal(path, this->events)) {
         this->events.clear(); // Ensure events are cleared on failure
         return false;
     }
     return true;
 }

// Renamed original saveToFile to SaveMacro - only saves, doesn't affect state
bool MacroCore::SaveMacro(const std::string& path) {
    json j;
    j["version"] = 1;
    json events_array = json::array();

    // Use the current state of the 'events' member variable for saving
    // Consider adding locking if saving can happen while recording (not current design)
    for (const auto& ev : events) {
        json event_obj;
        event_obj["time"] = ev.time; // Store relative time

        std::visit([&](auto&& arg) {
            using T = std::decay_t<decltype(arg)>;
            if constexpr (std::is_same_v<T, KeyboardEvent>) {
                event_obj["type"] = "key"; 
                event_obj["keyCode"] = arg.keyCode;
                event_obj["state"] = keyStateToString(arg.state);
            } else if constexpr (std::is_same_v<T, MouseMoveEvent>) {
                event_obj["type"] = "mouseMove";
                event_obj["x"] = arg.x;
                event_obj["y"] = arg.y;
            } else if constexpr (std::is_same_v<T, MouseClickEvent>) {
                event_obj["type"] = "mouseClick";
                event_obj["button"] = mouseButtonToString(arg.button);
                event_obj["state"] = keyStateToString(arg.state);
                event_obj["x"] = arg.x; 
                event_obj["y"] = arg.y;
            } else if constexpr (std::is_same_v<T, GotoEvent>) {
                event_obj["type"] = "goto";
                event_obj["targetLine"] = arg.targetLineNumber;
            }
        }, ev.event_data);
        events_array.push_back(event_obj);
    }

    j["events"] = events_array;

    try {
        std::ofstream file(path);
        if (!file.is_open()) {
            std::cerr << "Error opening file for writing: " << path << std::endl;
            return false;
        }
        file << j.dump(4); // Pretty print with 4 spaces indent
        file.close();
        return true;
    } catch (const std::exception& e) {
        std::cerr << "Error writing JSON to file: " << e.what() << std::endl;
        return false;
    }
}

// saveToFile is now just an alias or removed if not needed elsewhere
// bool MacroCore::saveToFile(const std::string& path) {
//     return SaveMacro(path);
// }

// --- Hook Callbacks ---

LRESULT CALLBACK MacroCore::LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam) {
    if (nCode == HC_ACTION && instance != nullptr) {
        // Check if keyboard recording is enabled
        if (!instance->current_settings.recordKeystrokes) {
            return CallNextHookEx(NULL, nCode, wParam, lParam);
        }

        KBDLLHOOKSTRUCT* pKbStruct = (KBDLLHOOKSTRUCT*)lParam;
        bool isKeyDown = (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN);
        bool isKeyUp = (wParam == WM_KEYUP || wParam == WM_SYSKEYUP);

        if ((isKeyDown || isKeyUp) && instance->is_recording.load()) {
            // Only record if we want press duration or it's a key down event
            if (instance->current_settings.insertPressDuration || isKeyDown) {
                KeyboardEvent keyEvent;
                keyEvent.keyCode = pKbStruct->vkCode;
                keyEvent.state = isKeyDown ? KeyState::Down : KeyState::Up;

                MacroEvent event;
                event.event_data = keyEvent;
                auto now = std::chrono::high_resolution_clock::now();
                auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(
                    now - instance->recording_start_time_point);
                event.time = static_cast<unsigned long>(duration.count());

                instance->events.push_back(event);
            }
        }
    }
    return CallNextHookEx(NULL, nCode, wParam, lParam);
}

LRESULT CALLBACK MacroCore::LowLevelMouseProc(int nCode, WPARAM wParam, LPARAM lParam) {
    if (nCode == HC_ACTION && instance != nullptr && instance->is_recording.load()) {
        MSLLHOOKSTRUCT* pMouseStruct = (MSLLHOOKSTRUCT*)lParam;
        MacroEvent event;
        auto now = std::chrono::high_resolution_clock::now();
        auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(
            now - instance->recording_start_time_point);
        event.time = static_cast<unsigned long>(duration.count());

        switch (wParam) {
            case WM_LBUTTONDOWN:
            case WM_LBUTTONUP:
            case WM_RBUTTONDOWN:
            case WM_RBUTTONUP:
            case WM_MBUTTONDOWN:
            case WM_MBUTTONUP:
            case WM_XBUTTONDOWN:
            case WM_XBUTTONUP:
                if (instance->current_settings.recordMouseClicks) {
                    MouseClickEvent clickEvent;
                    clickEvent.x = pMouseStruct->pt.x;
                    clickEvent.y = pMouseStruct->pt.y;

                    // Determine button and state
                    switch (wParam) {
                        case WM_LBUTTONDOWN:
                            clickEvent.button = MouseButton::Left;
                            clickEvent.state = KeyState::Down;
                            break;
                        case WM_LBUTTONUP:
                            clickEvent.button = MouseButton::Left;
                            clickEvent.state = KeyState::Up;
                            break;
                        case WM_RBUTTONDOWN:
                            clickEvent.button = MouseButton::Right;
                            clickEvent.state = KeyState::Down;
                            break;
                        case WM_RBUTTONUP:
                            clickEvent.button = MouseButton::Right;
                            clickEvent.state = KeyState::Up;
                            break;
                        case WM_MBUTTONDOWN:
                            clickEvent.button = MouseButton::Middle;
                            clickEvent.state = KeyState::Down;
                            break;
                        case WM_MBUTTONUP:
                            clickEvent.button = MouseButton::Middle;
                            clickEvent.state = KeyState::Up;
                            break;
                        case WM_XBUTTONDOWN:
                        case WM_XBUTTONUP:
                            clickEvent.button = HIWORD(pMouseStruct->mouseData) == XBUTTON1 ? 
                                MouseButton::X1 : MouseButton::X2;
                            clickEvent.state = (wParam == WM_XBUTTONDOWN) ? 
                                KeyState::Down : KeyState::Up;
                            break;
                    }

                    event.event_data = clickEvent;
                    instance->events.push_back(event);
                }
                break;

            case WM_MOUSEMOVE:
                if (instance->current_settings.recordAbsoluteMovement) {
                    MouseMoveEvent moveEvent;
                    moveEvent.x = pMouseStruct->pt.x;
                    moveEvent.y = pMouseStruct->pt.y;
                    event.event_data = moveEvent;
                    instance->events.push_back(event);
                }
                // Note: Relative movement would need to track previous position and calculate delta
                break;
        }
    }
    return CallNextHookEx(NULL, nCode, wParam, lParam);
}

// Add settings methods implementation
void MacroCore::setRecordingSettings(const RecordingSettings& settings) {
    current_settings = settings;
}

const MacroCore::RecordingSettings& MacroCore::getRecordingSettings() const {
    return current_settings;
}

// Add playback mode methods implementation
void MacroCore::setPlaybackMode(PlaybackMode mode) {
    current_playback_mode = mode;
}

MacroCore::PlaybackMode MacroCore::getPlaybackMode() const {
    return current_playback_mode;
} 