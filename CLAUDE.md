# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Windows tray utility (C# + WPF, `net8.0-windows`, x64 only) that watches global keyboard input and beeps / auto-corrects when a word looks typed in the wrong layout (**RU ↔ EN**). Built against the .NET 10 SDK pinned in [global.json](global.json) (via `rollForward: latestFeature`), but targets **.NET 8 Windows Desktop Runtime** at runtime.

User-facing README ([README.md](README.md)) is in Russian and is the most accurate spec of user behavior and settings semantics — prefer it over re-deriving behavior from code when answering user-level questions.

## Commands

```bash
dotnet build -c Release                      # builds src/KeyboardSwitch/bin/x64/Release/net8.0-windows/KeyboardSwitch.exe
dotnet run --project src/KeyboardSwitch      # run from source
dotnet run --project src/KeyboardSwitch -- --tray  # start minimized to tray (used by autostart)
```

There is **no test project and no linter configured** — don't invent test/lint commands. Solution has only `Debug|x64` and `Release|x64` — no `AnyCPU`.

## Architecture

### Pipeline (hot path)

All runtime services are wired by hand in [App.xaml.cs](src/KeyboardSwitch/App.xaml.cs) `OnStartup`. There is no DI container. The core data flow is one direction:

```
WH_KEYBOARD_LL hook  →  KeyboardHook (marshals to WPF Dispatcher)
        →  InputMonitor (VK → char via ToUnicodeEx with the foreground window's HKL)
        →  WordBuffer (accumulates until word terminator)
        →  TrigramLayoutDetector (BigramModel.Score on word vs. LayoutMap-swapped word)
        →  SoundService.PlayAlert  +  optional AutoSwitchService.FixWord
                                         (Backspace×N → ActivateLayout → SendInput Unicode)
```

Two invariants worth keeping in mind when editing:

1. **The LL hook callback must do minimal work.** [KeyboardHook](src/KeyboardSwitch/Interop/KeyboardHook.cs) intentionally marshals processing to the WPF Dispatcher; the native proc delegate is held in a field so the GC cannot collect it while the hook is installed. Don't inline heavy work into the callback, and don't let the proc delegate become a local.
2. **Character translation must use the *foreground window's* layout, not the thread's.** [InputMonitor.TranslateToChar](src/KeyboardSwitch/Services/InputMonitor.cs) calls `ToUnicodeEx` with `HKL` from `LayoutService.GetActiveLayout` (which walks `GetForegroundWindow` → `GetWindowThreadProcessId` → `GetKeyboardLayout`). The full `GetKeyboardState` must be read each call so Shift/Caps are honored.

### Layout detection model

[TrigramLayoutDetector](src/KeyboardSwitch/Services/LayoutDetector.cs) (name is historical — it's a bigram model) scores the typed word against [BigramModel](src/KeyboardSwitch/Services/BigramModel.cs) for the current language, then scores the *swapped* word (via [LayoutMap](src/KeyboardSwitch/Interop/LayoutMap.cs) ЙЦУКЕН ↔ QWERTY) against the other language's model, and compares the delta to a sensitivity threshold (`Low=2.0 / Medium=1.0 / High=0.5`). Corpora live in [LanguageCorpora.cs](src/KeyboardSwitch/Services/LanguageCorpora.cs) and are tiny (~200 words/language) — **accuracy is bounded by the corpus, not the algorithm**. If asked to improve accuracy, the leverage point is replacing the corpus with real frequency tables, not tuning the model.

Only **EN (0x0409)** and **RU (0x0419)** are wired up end-to-end — adding a language requires extending both [LayoutMap](src/KeyboardSwitch/Interop/LayoutMap.cs) and [LanguageCorpora](src/KeyboardSwitch/Services/LanguageCorpora.cs), plus the switch in [LayoutService.GetActiveLanguage](src/KeyboardSwitch/Services/LayoutService.cs).

### Word boundaries & modifier handling

[WordBuffer](src/KeyboardSwitch/Services/WordBuffer.cs) is the single authority on what ends a word. Any Ctrl/Alt/Win combo **clears the buffer** (shortcut context — never trigger detection on `Ctrl+S` etc.). Backspace shrinks the buffer but is not a terminator. Non-letter produced chars (space, punctuation) complete the word; no-char VKs (arrows, Enter, Tab, Esc, Home/End/PgUp/PgDn, Del/Ins) also complete it via the `IsWordTerminator` whitelist.

### Settings & persistence

- User settings: `%AppData%\KeyboardSwitch\settings.json`, loaded/saved by [JsonSettingsService](src/KeyboardSwitch/Services/SettingsService.cs) ([AppSettings](src/KeyboardSwitch/Models/AppSettings.cs)). Services never cache fields — they read `_settings.Current.*` on each event, so toggling from the tray takes effect immediately.
- Autostart: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\KeyboardSwitch` via [RegistryAutoStartService](src/KeyboardSwitch/Services/AutoStartService.cs), launched with `--tray`.
- Single-instance: named kernel mutex in [SingleInstanceGuard](src/KeyboardSwitch/Services/SingleInstance.cs); a second launch signals the first to show its window and then exits.
- Alert sound: generated on first run by [WavGenerator](src/KeyboardSwitch/Services/WavGenerator.cs) at `Resources\alert.wav` next to the exe. [SoundService](src/KeyboardSwitch/Services/SoundService.cs) falls back to `SystemSounds.Exclamation` if the WAV is missing or unreadable — keep that fallback path intact.

### UI

- [Views/TrayIcon.cs](src/KeyboardSwitch/Views/TrayIcon.cs) uses **WinForms `NotifyIcon`** (the project enables both `UseWPF` and `UseWindowsForms`). Fully-qualify `System.Windows.Application` / `System.Windows.MessageBox` when needed — the `using` of both namespaces in the same file will otherwise collide.
- [SettingsWindow](src/KeyboardSwitch/Views/SettingsWindow.xaml) binds to [SettingsViewModel](src/KeyboardSwitch/ViewModels/SettingsViewModel.cs). The window is created lazily and reused across tray clicks.

### Known platform limits (don't try to "fix" these)

- Elevated (UAC) windows and some games don't receive `SendInput` and don't deliver events to our LL hook — this is a Windows isolation boundary, not a bug.
- Windows does not expose "this is a password field" to a generic LL hook. The only mitigation is the `IgnoredProcesses` list in settings — by default keepass / 1password / bitwarden / lastpass.
- After an auto-fix, the terminator the user typed (space, `,`, `.`, …) can't be faithfully reproduced without extra state; the current compromise in [InputMonitor.OnWordCompleted](src/KeyboardSwitch/Services/InputMonitor.cs) erases `word + 1 separator` and re-types `swappedWord + space`. Changing this contract means also changing [AutoSwitchService.FixWord](src/KeyboardSwitch/Services/AutoSwitchService.cs).
