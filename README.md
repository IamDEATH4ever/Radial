# Radial

A universal radial menu for Windows.

Radial lets you open a context-sensitive radial menu using a mouse-button combination and quickly execute keyboard shortcuts or recorded macros without relying on application-specific mouse software.

> Hold **M4 + Right Click** to open Radial.

---

## Features

- Universal radial menu for Windows
- Global **M4 + Right Click** activation
- Cursor-relative menu positioning
- Keyboard shortcut macros
- Application-specific profiles
- Different shortcut sets for different applications
- Multiple radial wheels per application
- Up to **12 shortcuts per wheel**
- Switch between wheels using the mouse wheel
- Record keyboard shortcuts manually
- Macro naming
- Run/test macros from the Macro Manager
- Automatically uses the profile belonging to the current foreground application
- Mouse input is isolated while interacting with the radial menu
- Keyboard-only macro recording
- Renderer abstraction for future visual improvements

---

## How It Works

Radial runs in the background and monitors the global input state.

When the activation combination is detected:

```
M4 + Right Click
       │
       ▼
   Radial opens
       │
       ▼
Foreground application detected
       │
       ▼
Application profile loaded
       │
       ▼
Correct radial wheel displayed
       │
       ▼
Select a shortcut
       │
       ▼
Keyboard shortcut executed
```
