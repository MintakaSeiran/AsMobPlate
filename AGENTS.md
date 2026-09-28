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
- Preserve each delivered version on a separate branch named `release/<normalized assembly version>`, for example `release/1.0.18.0` for project version `1.0.018`, and push that branch when publishing is requested. Continue new revisions from the latest delivered version so changes are retained.
- For future revisions, implement and test locally, then run `scripts/Build-Plugin.ps1 -Deploy` to package and update the local development installation. Ask the user to reload the plugin and test in game. Do not push, tag or publish until the user explicitly approves publication after testing (or explicitly requests an untested prerelease).
- On approved publication, keep `main` at the latest approved release and push its `release/<normalized assembly version>` branch. Publish the exact tested ZIP and SHA256 checksum as GitHub Release assets and update the README download link. Preserve previous version branches and never force-push over unrelated remote changes.
- Do not treat automated test success or copying the DLL as in-game approval. Clearly distinguish files deployed on disk from the version currently loaded by Dalamud. Back up the dev installation before replacing files and preserve user configuration and logs.
