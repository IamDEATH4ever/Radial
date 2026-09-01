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

```text
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
````

The radial menu is positioned around the current cursor location, allowing it to be used without moving the mouse to a fixed location.

---

## Application Profiles

Radial keeps shortcuts separated by application.

For example:

```text
Blender
├── Undo
├── Redo
├── Select All
├── Frame Selected
└── Custom Macro

Notepad
├── Undo
├── Redo
├── Select All
└── Find

Chrome
├── New Tab
├── Close Tab
├── Reopen Tab
└── Refresh
```

for example :
A Blender shortcut will only appear when Blender is the active application.

This prevents unrelated shortcuts from being mixed together.

---

## Radial Wheels

Each application can have multiple wheels.

A wheel can contain up to **12 shortcuts**.

For example:

```text
Blender
│
├── Wheel 1
│   ├── Undo
│   ├── Redo
│   ├── Select All
│   ├── Delete
│   └── ...
│
├── Wheel 2
│   ├── Apply Transform
│   ├── Frame Selected
│   ├── Duplicate
│   └── ...
│
└── Wheel 3
    └── Custom shortcuts
```

When more than 12 shortcuts are needed, another wheel can be created.

Wheels can be switched using the mouse wheel while Radial is open.

---

## Macros

Radial records **keyboard shortcuts**, rather than recording mouse movement or arbitrary mouse actions.

For example:

```text
Ctrl + Z
```

is stored as a keyboard shortcut and can later be executed through Radial.

The Macro Manager allows shortcuts to be:

* Recorded
* Named
* Tested
* Deleted
* Assigned to application profiles
* Added to radial wheels

---

## Activation

Default activation:

```text
M4 + Right Click
```

The radial menu remains open while the activation interaction is being used and closes when the interaction is released.

The global input hook is designed so that mouse input returns to normal after Radial closes.

---

## Architecture

Radial is designed around separation between input, application logic, and rendering.

```text
Global Input
     │
     ▼
InputManager
     │
     ▼
RadialMenu
     │
     ├── Profiles
     ├── Wheels
     └── Macros
     │
     ▼
IRadialRenderer
     │
     ▼
Radial UI
```

The renderer is intentionally separated from the core radial-menu logic so the visual implementation can evolve without rewriting the input and profile systems.

---

## Technology

Radial is currently built with:

* **C#**
* **.NET 10**
* **WPF**
* Windows global input hooks
* SkiaSharp-based rendering

The project is currently Windows-only.

---

## Requirements

### Operating System

* Windows 10/11
* Windows x64

### Development

To build the project from source, install:

* .NET 10 SDK
* Visual Studio 2022 or another compatible C# IDE
* Git

---

## Building

Clone the repository:

```bash
git clone https://github.com/IamDEATH4ever/Radial.git
cd Radial
```

Build:

```bash
dotnet build
```

Run:

```bash
dotnet run
```

---

## Current Status

Radial is currently in active development.

The core concept is functional:

* Global activation
* Radial interaction
* Application profiles
* Wheels
* Keyboard shortcut macros
* Macro recording
* Macro execution
* Application-specific shortcut management

The visual system is still being refined.

### Planned

* More polished radial visuals
* Better animations
* Custom themes
* Improved icons
* More configuration options
* Additional activation combinations
* Better macro management
* Import/export of profiles
* More robust application detection
* Custom radial layouts

---

## Why Radial?

Many gaming mice provide radial menus through proprietary software, but those features are often tied to a specific manufacturer's ecosystem.

Radial aims to make the concept **mouse-independent**.

Instead of:

```text
Logitech mouse
      ↓
Logitech software
      ↓
Radial menu
```

Radial aims for:

```text
Any mouse
   ↓
Windows input
   ↓
Radial
   ↓
Application-specific shortcuts
```

The goal is to make a fast radial workflow available regardless of which mouse a user owns.

---

## Contributing

Radial is currently primarily a personal development project, but contributions, ideas, bug reports, and improvements are welcome.

If you find a bug, please open an issue with:

* Windows version
* Radial version/commit
* Application where the issue occurred
* Steps to reproduce
* Relevant logs or screenshots

---

## License

License information will be added as the project matures.

---

## Project

**Radial**
A universal, application-aware radial shortcut menu for Windows.

Built with C# and .NET.
