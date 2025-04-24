#pragma once

#include <string>
#include <variant>

// Represents different types of macro events

enum class MouseButton {
    Left,
    Right,
    Middle,
    X1,
    X2
};

enum class KeyState {
    Down,
    Up
};

struct KeyboardEvent {
    int keyCode; // Virtual-Key Code
    KeyState state;
};

struct MouseMoveEvent {
    int x;
    int y; // Absolute screen coordinates
};

struct MouseClickEvent {
    MouseButton button;
    KeyState state;
    int x; // Position where the click occurred
    int y;
};

// Added: Event for Goto instruction
struct GotoEvent {
    int targetLineNumber; // 1-based line number from the UI
};

// Using std::variant to hold different event types
using EventData = std::variant<KeyboardEvent, MouseMoveEvent, MouseClickEvent, GotoEvent>;

struct MacroEvent {
    EventData event_data;
    unsigned long time; // Timestamp in milliseconds since recording started
};

// Helper functions to convert enums to/from strings for serialization
std::string mouseButtonToString(MouseButton button);
MouseButton stringToMouseButton(const std::string& str);

std::string keyStateToString(KeyState state);
KeyState stringToKeyState(const std::string& str); 