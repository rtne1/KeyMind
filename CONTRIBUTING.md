# Contributing

Thank you for helping improve KeyMind. This repository is currently private; external contributions are not yet open. Once released, please follow these guidelines.

## Before you start

1. Search existing issues for duplicates.
2. For behavioral changes, open an issue with the affected keyboard layout, application, expected result and actual result.
3. Never include passwords, private messages, raw keystroke captures or identifiable logs in public issues.
4. Keep unrelated changes in separate pull requests; do not commit build outputs or personal dictionaries.

## Development workflow

- Windows application source: `src/`, `KeyMind.csproj`.
- Legacy Windows build: `Build-KeyMind.cmd`.
- Tests: `python tests/verify_package.py`, `python tests/verify_background_listener.py`, `python tests/eval_accuracy.py`.
- On Windows, run `TEST-ENGINE.cmd` and test typing in Notepad, Chrome and the relevant editor.
- Update documentation and test cases for behavior changes.

## Pull requests

Explain **why** the change is necessary, how it was tested, any backwards-compatibility concerns, and whether it changes how keyboard data is observed, stored or transmitted. Bug fixes should include a minimal regression test when practical.

## Code style

Follow the repository's `.editorconfig`. Keep C# code compatible with the existing Windows compiler path when touching legacy build flows; do not silently break `Build-KeyMind.cmd` while changing `KeyMind.csproj`.

## Dependencies and licensing

Before adding a library or word list, document its upstream source, applicable license and redistribution terms. Do not add copyrighted dictionaries without permission.

## Code of conduct

Participation is subject to [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).