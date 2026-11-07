# Security policy

## Supported version

| Version | Status |
| --- | --- |
| 3.0.x | Development preview; best-effort fixes |
| Older versions | No committed security maintenance |

## Responsible reporting

Please **do not open a public issue** to disclose a vulnerability or attach sensitive diagnostic content. After the GitHub repository is created, use **Security → Report a vulnerability** if private vulnerability reporting is enabled, or contact the repository owner through an established private channel.

Include reproduction steps, affected Windows version, application, expected behavior and impact. Do not send password or keystroke captures.

## Threat model and limitations

- KeyMind installs a low-level Windows keyboard hook to implement cross-application layout correction. This is sensitive functionality and should be reviewed before deployment.
- The app may observe input in contexts that it cannot identify as a password or otherwise sensitive field. App exclusion rules are defense-in-depth, **not** a guarantee.
- Simulated input can behave differently across elevated, protected or custom UI applications.
- Local settings and errors are stored in `%LOCALAPPDATA%\KeyMind`.
- No independent security audit or code signing is claimed.

**Safety recommendation:** Pause or close KeyMind for passwords and sensitive information; inspect all logs before sharing them.