# Contributing to Resilite

Thank you for your interest in contributing to Resilite
We welcome bug reports, improvements, documentation updates, and new ideas.

This guide explains how to set up the project locally and how to submit changes properly.

---

## Code of Conduct

By participating in this project, you agree to follow the Code of Conduct.
Please be respectful and constructive in all interactions.

---

## Prerequisites

- .NET SDK (see `global.json` if present)
- Git

Verify your installation:

```bash
dotnet --version
```

---

## Clone the Repository

```bash
git clone https://github.com/ferdikurnazdm/Resilite.git
cd Resilite
```

---

## Build the Project

```bash
dotnet build
```

---

## Run Tests

```bash
dotnet test
```

All tests must pass before submitting a Pull Request.

---

## Code Style & Conventions

- Follow existing code conventions and naming patterns.
- Keep the public API consistent with the current design.
- Avoid unnecessary breaking changes.
- Prefer clarity over cleverness.

If formatting tools are available:

```bash
dotnet format
```

---

## Documentation

If your change affects:

- Public APIs
- Behavior
- Usage patterns

Please update:

- Relevant documentation under `doc/`
- `CHANGELOG.md` (if applicable)

---

## Branching Strategy

Use descriptive branch names:

- `feature/...`
- `fix/...`
- `docs/...`
- `refactor/...`

Example:

```
feature/add-mapasync
```

---

## Submitting a Pull Request

Before opening a PR, ensure:

- [ ] The project builds successfully
- [ ] Tests pass
- [ ] Tests added/updated (if applicable)
- [ ] Documentation updated (if applicable)
- [ ] `CHANGELOG.md` updated (if applicable)
- [ ] No breaking changes OR breaking changes clearly documented
- [ ] CI checks are passing

Provide a clear description of:

- What changed
- Why it changed
- Any potential impact

---

## Reporting Issues

Please use the provided issue templates:

- Bug report
- Feature request

When reporting a bug, include:

- Steps to reproduce
- Expected behavior
- Actual behavior
- Resilite version
- Environment details (.NET version, OS, etc.)
- A minimal reproducible example if possible

---

## Versioning & Breaking Changes

Resilite follows Semantic Versioning (SemVer):

- PATCH: backward-compatible bug fixes
- MINOR: backward-compatible features
- MAJOR: breaking changes

Breaking changes must:

- Be clearly documented in `CHANGELOG.md`
- Include migration guidance when possible

---

Thank you for helping improve Resilite
