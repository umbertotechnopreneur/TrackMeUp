# TrackMeUp Security Policy

## Supported Versions

Security fixes are considered for:

- the default branch;
- the latest published release (when available).

TrackMeUp is still evolving, so behavior may change between revisions.

## Reporting a Vulnerability

Do not open a public issue for suspected vulnerabilities.

Report privately to **hello@umbertogiacobbi.biz** with subject:

`TrackMeUp security report`

Include, when possible:

- affected commit/version;
- component (for example capture pipeline, AI provider adapter, retention, CLI, installer);
- reproduction steps;
- security impact and prerequisites;
- redacted logs or proof of concept;
- possible workaround.

## Sensitive Data and Secrets

Never publish:

- API keys, bearer tokens, connection strings, or private endpoints;
- personal screenshots or reports with sensitive data;
- private local/UNC paths when avoidable;
- machine-specific diagnostic data that can expose identities.

If a secret was ever committed, assume it is compromised:

1. rotate or revoke it;
2. remove it from history where applicable;
3. document the remediation.

## Security-Sensitive Areas

Call out changes in PRs when touching:

- screenshot capture and retention flow;
- AI provider request pipeline and transport;
- local storage, redaction, and diagnostics export;
- runtime ownership, mutex, named-pipe, startup flow;
- packaging/release scripts and distribution artifacts.

## Scope of Public Issues

Feature requests, standard bugs, and usage questions belong in public issues or `SUPPORT.md`.
