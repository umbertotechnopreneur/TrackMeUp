# TrackMeUp governance

TrackMeUp is an open-source project maintained by
[@umbertotechnopreneur](https://github.com/umbertotechnopreneur). This document
explains how changes are proposed, reviewed, and accepted.

## Project stewardship

The maintainer sets product direction, protects the privacy and local-first
contracts, reviews contributions, manages releases, and makes final decisions
when consensus is not available.

Repository ownership does not override the MIT license. Contributions accepted
into the project are licensed as described in [CONTRIBUTING.md](CONTRIBUTING.md),
while the TrackMeUp name and brand assets remain governed by
[TRADEMARKS.md](TRADEMARKS.md).

## Proposing changes

- Use GitHub Discussions for questions, early ideas, and design exploration.
- Use the issue forms for reproducible bugs, focused feature requests, and
  documentation problems.
- Report vulnerabilities privately according to [SECURITY.md](SECURITY.md).
- Open an issue before a large architectural, privacy, persistence, protocol,
  packaging, or dependency change.

## Accepting changes

Every change to `main` is made through a pull request. A pull request must be
focused, pass the required checks, resolve review conversations, and satisfy the
repository contribution and provenance requirements.

TrackMeUp currently has one maintainer, so branch protection does not require an
approval that the pull-request author cannot provide to themselves. The
maintainer still reviews the complete diff and validation evidence before using
squash merge. External contributions are reviewed by the maintainer.

Force pushes and branch deletion are disabled for `main`. A linear history is
kept, and merged topic branches are deleted automatically.

## Decisions and releases

Routine decisions are recorded in issues and pull requests. Material product or
architecture decisions should update the relevant durable documentation in the
same pull request.

Releases are created by the maintainer after the protected checks pass and the
package, privacy, licensing, and provenance requirements have been reviewed.

## Conduct

Participation is governed by [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). The
maintainer may edit, close, or reject contributions that violate the Code of
Conduct, expose sensitive information, or conflict with the project's safety,
privacy, licensing, or product boundaries.
