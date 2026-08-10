# D1 — Stack applicative : WPF / .NET 8 (C#) — DÉCIDÉ

Contexte : interop Win32 profonde (event hooks, DPAPI, COM Outlook, WebView2), installation sans droits admin, mainteneur unique. Décision : WPF sur .NET 8, distribution self-contained per-user. Justification : documentation la plus riche, interop native, un seul langage, WebView2 intégré (requis par D4). Rejeté : Tauri v2 (double compétence Rust+Web, interop Win32 moins documentée), Electron (empreinte résidente excessive pour un agent permanent).
