# How WorkTrail is maintained

I'm [Umberto Giacobbi](https://github.com/umbertotechnopreneur), the maintainer
of WorkTrail. I develop it with help from a few contributors. This guide explains
how to suggest changes and how I decide what goes into the app.

## Who looks after the project

I decide what to work on, review contributions, and publish releases. I check
that changes respect the app's privacy rules and keep data local by default.
When a discussion doesn't reach agreement, I make the final call.

Repository ownership does not override the MIT license. Contributions accepted
into the project are licensed as described in [CONTRIBUTING.md](CONTRIBUTING.md),
while the WorkTrail name and brand assets remain governed by
[TRADEMARKS.md](TRADEMARKS.md).

## Suggesting a change

- Use GitHub Discussions for questions and early ideas.
- Use the issue forms for bugs, specific feature requests, and documentation
  problems. For bugs, include the steps needed to reproduce them.
- Report vulnerabilities privately according to [SECURITY.md](SECURITY.md).
- Open an issue before a large change to the app's structure, privacy, storage,
  process communication, packaging, or dependencies.

## Reviewing and merging changes

Every change to `main` goes through a pull request. Keep it focused, make sure
the required checks pass, and resolve review comments. Contributions must also
follow the contributor rules and explain where any added material came from.

WorkTrail currently has one maintainer. GitHub doesn't allow authors to approve
their own pull requests, so a separate approval isn't required. The maintainer
still reviews every change and its test results before merging it as a single
commit (squash merge). They review external contributions too.

Force pushes and deletion are disabled for `main`. Merges keep a linear history,
and branches are deleted automatically after merging.

## Decisions and releases

I record decisions in issues and pull requests. If a decision changes how the
app works or how the code is organized, update the relevant guide in the same
pull request.

The maintainer publishes releases after the required checks pass and after
reviewing the package, privacy behavior, licenses, and sources of included material.

## Conduct

Follow the [Code of Conduct](CODE_OF_CONDUCT.md). The maintainer may edit, close,
or reject contributions that break those rules, expose sensitive information,
or conflict with the project's safety, privacy, licensing, or product decisions.
