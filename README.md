# KeyboardSwitch

Windows-утилита на **C# + WPF (.NET 8, x64)**, которая отслеживает ввод и подаёт звуковой сигнал, если слово, похоже, набрано в неверной раскладке (**RU ↔ EN**). Живёт в трее, стартует с Windows (опция), опционально умеет авто-исправлять слово.

## Как это работает

Windows не имеет встроенной детекции «неверной раскладки», поэтому логика реализована вручную:

1. Глобальный хук `WH_KEYBOARD_LL` (user32) читает все нажатия системы.
2. VK-коды переводятся в символы через `ToUnicodeEx` с активной раскладкой.
3. [WordBuffer](src/KeyboardSwitch/Services/WordBuffer.cs) собирает текущее слово до разделителя (пробел, Enter, Tab, знаки, навигация).
4. [TrigramLayoutDetector](src/KeyboardSwitch/Services/LayoutDetector.cs) сравнивает log-likelihood слова под моделью текущего языка и под моделью слова, «транслитерированного» в другую раскладку через [LayoutMap](src/KeyboardSwitch/Interop/LayoutMap.cs) (ЙЦУКЕН ↔ QWERTY).
5. Если «чужая» раскладка даёт заметно более высокий score — срабатывает сигнал, а при включённой опции — авто-исправление через `SendInput` (Backspace×N → смена раскладки → набор через Unicode-ввод).

## Сборка

Требуется **.NET 8 Windows Desktop Runtime** (или .NET 10 SDK с совместимостью — `global.json` уже прописан).

```
dotnet build -c Release
```

Вывод: [src/KeyboardSwitch/bin/x64/Release/net8.0-windows/KeyboardSwitch.exe](src/KeyboardSwitch/bin/x64/Release/net8.0-windows/).

## Запуск

```bash
# Разработка
dotnet run

# Публикация в единый .exe без зависимостей
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

или просто запустить `KeyboardSwitch.exe`. При первом запуске — откроется окно настроек. Дальше программа живёт в трее; клик по иконке → окно настроек.

Параметры командной строки:

- `--tray` — стартовать сразу в трее, без окна. Используется автозагрузкой.

## Настройки

Хранятся в `%AppData%\KeyboardSwitch\settings.json`:

| Поле               | Описание                            | По умолчанию                            |
| ------------------ | ----------------------------------- | --------------------------------------- |
| `enabled`          | Включён ли мониторинг               | `true`                                  |
| `playSound`        | Играть звуковой сигнал              | `true`                                  |
| `autoSwitch`       | Авто-исправлять слово               | `false`                                 |
| `autoStart`        | Запуск с Windows (реестр)           | `false`                                 |
| `customSoundPath`  | Путь к своему WAV                   | `null`                                  |
| `minWordLength`    | Минимальная длина слова для анализа | `4`                                     |
| `sensitivity`      | `Low` / `Medium` / `High`           | `Medium`                                |
| `ignoredProcesses` | Имена exe, где не отслеживать       | keepass, 1password, bitwarden, lastpass |

Автозагрузка пишется в `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\KeyboardSwitch`.

## Звук

При первом запуске рядом с exe создаётся `Resources\alert.wav` (короткий двух-тональный сигнал, генерируется [WavGenerator](src/KeyboardSwitch/Services/WavGenerator.cs)). Файл можно заменить своим, либо указать путь в настройках. Если WAV недоступен — используется `SystemSounds.Exclamation` как fallback.

## Известные ограничения

- Окна с повышенными правами (UAC) и некоторые игры не принимают ввод от `SendInput` / не видят наш хук. Это ограничение Windows.
- Триграммная модель обучена на маленьком встроенном корпусе (~200 слов на язык). Для большей точности можно сгенерировать полноценные частотные таблицы и подменить модель в [LayoutDetector](src/KeyboardSwitch/Services/LayoutDetector.cs).
- Поддерживаются только раскладки EN (0x0409) и RU (0x0419). Для других языков нужно расширить [LayoutMap](src/KeyboardSwitch/Interop/LayoutMap.cs) и добавить корпус.
- Пароли: по умолчанию игнорируются процессы популярных менеджеров паролей. В обычных текстовых полях (например, поле «Пароль» в браузере) Windows не сообщает, что это пароль — ввод идёт в буфер как обычный текст. Используйте `ignoredProcesses` для чувствительных приложений.

## Структура

```
src/KeyboardSwitch/
├── Interop/       WH_KEYBOARD_LL, LayoutMap (RU↔EN), P/Invoke
├── Services/      SettingsService, LayoutService, WordBuffer,
│                  BigramModel, LayoutDetector, SoundService,
│                  AutoStartService, AutoSwitchService,
│                  InputMonitor, SingleInstanceGuard, WavGenerator
├── Models/        AppSettings
├── ViewModels/    SettingsViewModel
├── Views/         App.xaml, SettingsWindow, TrayIcon (WinForms NotifyIcon)
└── Resources/     (tray.ico — опционально; alert.wav — генерируется на 1-м запуске)
```
