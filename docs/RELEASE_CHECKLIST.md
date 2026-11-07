# Release readiness checklist

- [ ] Pass Python static, listener and accuracy checks.
- [ ] Pass Windows Framework compile and C# smoke/parity tests.
- [ ] Pass `dotnet build -c Release` on Windows with .NET 10 SDK.
- [ ] Manually verify Notepad, Chrome, Word and a code editor on representative Windows systems.
- [ ] Test English → Arabic, Arabic → English, undo, pause, app exclusions and listener restart.
- [ ] Test password and privileged-application limitations; confirm user-facing warnings.
- [ ] Inspect source, documentation and release packages for secrets, logs and personal identifiers.
- [ ] Review all dictionary and brand-asset provenance and required third-party notices.
- [ ] Select a public software license; replace the provisional `LICENSE` before opening the repo.
- [ ] Approve the security reporting channel and code-signing/release policy.
- [ ] Produce a clean, reproducible binary; record checksums and publish only explicitly reviewed artifacts.
- [ ] Only after all checks, change GitHub visibility from **Private** to **Public** if desired.
