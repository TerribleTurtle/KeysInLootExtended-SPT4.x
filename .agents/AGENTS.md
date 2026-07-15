# Project-Scoped Rules

## SPT Build & Packaging Rules
- **Compilation is Mandatory:** This is a native .NET 9 C# mod for SPT 4.0+. You MUST compile the mod by executing `dotnet build .\KeysInLootExtended\KeysInLootExtended.csproj -c Release`.
- **Deterministic Packaging (`release.ps1`):** Do not manually attempt to zip files using `.buildignore` or manually ignoring dev files. To build and package a clean drag-and-drop `.zip` release without source files, simply execute `.\release.ps1`.
- **Packaging Structure:** The generated `.zip` automatically packages the mod inside the correct drag-and-drop structure: `user/mods/KeysInLootExtended/`.
- **Avoid "zz" Unless Necessary:** The `zz` prefix forces the mod to load last in SPT, but causes visual clutter. Do not use it unless explicitly requested.

## Deployment & Testing Rules
- **Live Mod Directory Strict Read-Only:** When interacting with the user's live SPT installation (`B:\Game Storage\SPT\SPT\user\mods`), treat the directory and all other mods as strictly read-only. Only modify files within our specific `KeysInLootExtended` mod directory.

## Communication & Etiquette Rules
- **Respect Original Authors:** WE DO NOT PUT DOWN THE ORIGINAL AUTHORS AT ALL. When referencing legacy code or original mods, always maintain a respectful and neutral tone. Never use disparaging adjectives (e.g., "slow legacy", "bad code").
