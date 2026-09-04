# Auto Typer byGo

**The ultimate offline productivity tool for Windows.**  
*Type less, do more. Automate your repetitive typing tasks with a single keystroke.*

---

## 🚀 Why Auto Typer?

**Auto Typer byGo** is designed for professionals who value speed, privacy, and control. Whether you're a support agent, coder, or writer, this tool saves you hours by expanding short hotkeys into full-length text snippets instantly.

### ✨ Key Features

*   **⚡ Instant Expansion**  
    Turn short key combos (e.g., `F8` or `Ctrl+Alt+T`) into emails, code blocks, or signatures instantly.

*   **🎮 Xbox Game Bar Overlay (`Win + G`)**  
    Trigger the Auto-Typer widget anywhere—over fullscreen games, browsers, coding sandboxes, and virtual machines. Select snippets or paste clipboard text, adjust speeds, start countdowns, and track typing progress without Alt-Tabbing!

*   **🔒 100% Offline & Private**  
    Your data never leaves your device. No cloud sync, no tracking, no subscription. **You own your data.**

*   **🕹️ Total Control & Safety**  
    *   **Manual Start/Stop:** Control typing directly from the desktop app or the Game Bar overlay.
    *   **Emergency Stop:** Press `Ctrl + Shift + Esc` (or `ESC` in-app) to abort an active typing job instantly.
    *   **Countdown Delay:** 2s/3s/5s delay allows focusing fullscreen target windows before keystrokes begin.

*   **🧠 Smart Typing Modes**  
    *   **Human-Like:** Simulates natural typing speeds with randomized human variance.
    *   **Paste:** Instantly injects large text blocks via clipboard simulation.
    *   **Fast:** Rapid sequential keystrokes.
    *   **Macro:** Supports automation tokens like `{ENTER}`, `{TAB}`, and `{DELAY 500}`.

---

## 📥 Installation

**Option 1: Installer (Recommended)**
1.  Download `AutoTyper-byGo-Setup.exe` from the [Releases Page](https://github.com/goutham-11-16/Auto-Typer/releases/latest).
2.  Run the installer. It will create a desktop shortcut and start menu entry.

**Option 2: Portable**
1.  Download the `.zip` or `.exe` (Self-Contained).
2.  Run it directly. No install needed.

> **System Requirements:** Windows 10 or Windows 11.

---

## 🗑️ Uninstalling

*   Go to **Settings > Apps > Installed Apps**.
*   Search for "Auto Typer".
*   Click **Uninstall**.
*   *(Note: This leaves your saved snippets intact in the `%AppData%` folder if you reinstall later!)*

---

## 🛠️ How to Use

### 1. Create a Snippet
*   Click the **+ Add** button in the sidebar.
*   Give it a **Name** (e.g., "Email Signature").
*   Type your **Content** in the editor.

### 2. Set a Hotkey
*   Click the **Hotkey Trigger** box.
*   Press your desired combination (e.g., `F9`, `Ctrl+Shift+S`).
*   *Tip: Use F-keys (F1-F12) for single-key speed!*

### 3. Choose a Mode
*   Select **Paste** for instant text or **Human-Like** for natural typing.

### 4. Save & Run
*   Click **Save Snippet**.
*   Ensure the service status is **Running** (Green indicator).
*   Open Notepad, Chrome, or any app and press your hotkey!

---

## 🤖 Macros & Tokens

Take automation further with built-in tokens:

| Token | Function | Example |
| :--- | :--- | :--- |
| `{ENTER}` | Simulates the Enter key | `Hello{ENTER}World` |
| `{TAB}` | Simulates the Tab key | `Username{TAB}Password` |
| `{DELAY X}` | Pauses for X milliseconds | `Wait...{DELAY 1000}Done!` |

---

## 🎮 Xbox Game Bar Widget Setup & Usage

The **Auto-Typer Game Bar Widget** lets you control typing over **fullscreen applications, games, coding platforms, and browsers** without switching windows or minimizing your current screen.

### 1. Build Both Projects
Run the automated build script from the root directory:
```cmd
build.bat
```
*(Or in PowerShell: `.\build.ps1 -Configuration Release`)*

This compiles:
1. `AutoTyper` (.NET 8.0 WPF desktop typing engine)
2. `GameBarWidget` (C++/WinRT UWP Xbox Game Bar extension)

### 2. Register the Widget with Windows
Register the widget into your local Xbox Game Bar menu:
```powershell
powershell -ExecutionPolicy Bypass -File .\Register-Widget.ps1
```

### 3. Open and Use the Widget
1. Ensure `AutoTyper-byGo.exe` is running (or click **"Launch Engine"** directly from the widget).
2. Press **`Win + G`** at any time to summon the Xbox Game Bar.
3. In the top Widget Menu, click **Auto-Typer**.
4. **Pin the Widget (Optional):** Click the **Pin** icon on the widget header so it stays floating on top when Game Bar closes!
5. **Type Code/Text:**
   - Pick a snippet from the dropdown, OR
   - Paste code from clipboard using the **"Clipboard"** button, OR
   - Type custom text into the editor.
6. **Set Speed & Delay:**
   - Choose typing mode (**HumanLike**, **Fast**, **Paste**, **Macro**).
   - Set speed (**Normal**, **Fast**, **Ultra**, **Slow**).
   - Set countdown delay (**3s Recommended** so you have time to click your target input field).
7. Click **`START TYPING`**! The countdown begins, live character progress updates on the progress bar, and typing safely completes.
8. **Pause / Resume / Stop:** Click **Pause** or **STOP** on the widget at any moment.

### 4. Unregistering the Widget
To remove the widget registration from Windows:
```powershell
powershell -ExecutionPolicy Bypass -File .\Unregister-Widget.ps1
```

---

## 🛑 Safety Features

*   **Stop Service:** Use the toggle button in the top bar to completely disable all hotkeys when you need to focus.
*   **Emergency Stop Hotkey:** Press **`Ctrl + Shift + Esc`** globally at any time to instantly abort an active typing task.
*   **In-App / Widget Stop:** Click the red **STOP** button on either the desktop UI or Game Bar widget.
*   **Window Focus Countdown:** Configurable 2s/3s/5s delay ensures you never type into the wrong window.

---

## 📄 License

This project is free for personal use.
© 2026 Goutham Reddy.
