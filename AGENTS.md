# AS Mob Plate Versioning

These rules apply to the AsMobPlate plugin, not unrelated projects in this workspace.

- The user requires a release version bump for every delivered plugin revision.
- The version format is `1.0.NNN`, with a three-digit, zero-padded decimal patch number.
- The current baseline is `1.0.005`; the next revision must be `1.0.006`, then `1.0.007`, and so on.
- Read the current `Version` in `AsMobPlate.csproj` before changing it. That value is authoritative; do not reset it to the baseline above.
- Increment the patch number once per delivered change, not for each edited file or repeated build of the same change.
- Update the current release in README.md with the project version.
- Let the SDK derive assembly and manifest versions. Numeric .NET/Dalamud versions may normalize `1.0.005` to `1.0.5.0`.
- Keep `Configuration.Version` independent: it is a configuration schema migration number.
- Run `dotnet build` after a change and verify the generated manifest version matches the DLL before delivery.
- Keep README.md headings, descriptions, and captions in English, and prefer English UI screenshots for the GitHub page. Preserve the plugin's multilingual UI support.
- Preserve each delivered version on a separate branch named `codex/v1.0.NNN` and push that branch when publishing is requested. Continue new revisions from the latest delivered version so changes are retained.
- Do not push or merge release changes into `main` unless the user explicitly requests it. Keep previous version branches intact.
