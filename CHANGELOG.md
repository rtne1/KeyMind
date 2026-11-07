# Changelog

Significant updates to KeyMind are recorded here. Dates of individual upstream development changes are not confirmed in the source package.

## [3.0.0] — Initial repository preparation

- Added two-sided recognition scoring for wrong-layout text, including candidate likelihoods.
- Reduced spurious conversion for rare English dictionary entries.
- Improved handling of English contractions and punctuation.
- Added optional compact binary `.kmd` dictionary support and language-specific custom dictionaries.
- Added normalization for Arabic diacritics and tatweel in lookups.
- Added automated package checks, Python reference evaluation and Windows parity test vectors.
- Updated keyboard listener recovery and diagnostic controls.
- Established repository documentation, CI checks and GitHub contribution templates.

### Known limits

- Some applications block injected keyboard input; actual behavior must be tested on Windows.
- Accuracy varies with vocabulary and language context, especially unseen Arabic words.
- Password-field detection cannot be treated as reliable across all apps.

For original technical notes see [release notes](docs/releases/RELEASE-NOTES-3.0.txt).