# QuickCapture — Windows

A tiny always-on capture bar for Windows. Press a hotkey, type a thought with
shorthand modifiers, hit Enter — it lands in your Notion database.

Built with **Avalonia (.NET 8)**. Framework-dependent build — requires the
[Microsoft .NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0)
on the target machine.

## Features

- **Global hotkey** `Ctrl+Shift+Space` opens the capture bar from anywhere
- **Enter** saves to Notion · **Shift+Enter** new line · **Esc** dismisses
- **Live syntax highlighting** for modifiers as you type
- **Attachments**: paste from clipboard, drag & drop, or file picker
  (images as thumbnails, files as extension badges)
- **System tray**: closing the window hides to tray; Quit from the tray menu
- **Notion integration** via HTTPS POST to `api.notion.com`

## Typing modifiers

| Modifier | Does | Notion column |
|---|---|---|
| `#1` `#2` `#3` (or `` `1 `` `` `2 `` `` `3 ``) | Priority P1 / P2 / P3 | Priority (select) |
| `#0` | Priority P0 | Priority (select) |
| `/urgent` `/high` `/medium` `/low`, `/p0`–`/p3` | Priority P0–P3 | Priority (select) |
| `~` | Opens topic list popup (Inbox, Education, Career, Fitness, Health, Personal) | Topic (text) |
| `#word` | Adds word to Topic | Topic (text) |
| `!` | Opens category popup (Spark, TIL, Prompt) | Category (select) |
| dates / times | e.g. `tomorrow`, `Friday 5pm` | Date (date) |

Only `#` followed by a single digit 0–3 is priority — `#work` is just a
topic tag. Unknown `!words` are left as plain text.

## How text lands in Notion

- **Name** ← first line (max 80 chars) · **Note** ← first line (max 2000 chars)
- **Page body** ← full text in 2000-char blocks; images first, then files, then text
- **Captured** ← save time · **Source** ← always `Quick Capture`

## Setup

1. Install the .NET 8 SDK.
2. Copy `config.example.json` to `config.json` next to the built exe and fill in
   your Notion integration token and database ID.
   (`config.json` is git-ignored — never commit your token.)
3. `dotnet build -c Release` (or open `src/` in your IDE).

## Version history

- **v7** — modifier scheme: `#1`/`#2`/`#3` and `` `1 ``/`` `2 ``/`` `3 `` set
  priority; `!` opens the Category popup (Spark, TIL, Prompt)
- **v6** — fixed editor typing bug (missing AvaloniaEdit theme)
- **v5** — teal lightning-bolt logo for taskbar and tray
- **v4** — colored modifiers + image previews
- **v3** — earlier iteration
