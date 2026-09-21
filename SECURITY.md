# Reporting a security problem

## Supported versions

I consider security fixes for:

- the default branch;
- the latest published release (when available).

WorkTrail is still in development, so behavior can change between versions.

## How to reach me

If you think you've found a vulnerability, please report it privately. Don't open a public issue with details that someone could use to exploit it.

Email **hello@umbertogiacobbi.biz** with the subject:

`WorkTrail security report`

Include what you can:

- the affected commit or app version;
- the part of the app involved, such as screenshots, AI requests, data deletion, the CLI, or the installer;
- steps to reproduce the problem;
- what someone could do with it and what access they would need;
- logs with private information removed, or a small example that demonstrates the problem;
- a workaround, if you know one.

## Keep private information out of reports

Never publish:

- API keys, bearer tokens, connection strings, or private endpoints;
- personal screenshots or reports with sensitive data;
- private file or network-share paths when avoidable;
- machine-specific diagnostic data that can expose identities.

If a secret was committed to Git, assume someone may have copied it:

1. Replace or revoke it.
2. Remove it from history where applicable.
3. Record what you did to fix the problem.

## Changes that need extra attention

Mention it clearly in your pull request if you change:

- how screenshots are taken, kept, or deleted;
- how requests are built and sent to an AI provider;
- local storage, removal of private information from logs, or diagnostics export;
- how the shared tracker starts and how processes connect through the mutex and named pipe;
- packaging, release scripts, or files included in a download.

## For other questions

For feature ideas, ordinary bugs, or help using the app, see [SUPPORT.md](SUPPORT.md).
