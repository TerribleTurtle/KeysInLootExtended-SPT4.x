# Project-Scoped Rules

## SPT Build & Packaging Rules
- **Compilation is Mandatory:** The SPT server does NOT compile `.ts` files on the fly. You MUST ensure that the build process compiles `.ts` files into `.js` files (using `npx tsc` or similar) so the final mod contains `src/mod.js`.
- **Packaging Structure:** The release `.zip` must package the mod inside a drag-and-drop structure: `SPT/user/mods/ModName/`.
- **Avoid "zz" Unless Necessary:** The `zz` prefix (e.g., `zzKeysInLootExtended`) forces the mod to load last in SPT, but causes visual clutter. Do not use it unless explicitly requested or mathematically required for override priority.
- **Ignore Source Files:** Ensure `.buildignore` ignores `**/*.ts` and other dev-only files (like `.gitignore`, `tsconfig.json`, `.agents`) so the final release zip is lightweight.
