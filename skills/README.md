# Agent Skills

This directory contains Agent Skills following the specification:
https://agentskills.io/specification

## Structure

Each skill lives in its own folder and must contain at least `SKILL.md`.

Example:

- `skills/<skill-name>/SKILL.md`
- `skills/<skill-name>/scripts/` (optional)
- `skills/<skill-name>/references/` (optional)
- `skills/<skill-name>/assets/` (optional)

## Validation

Use the reference validator:

```bash
skills-ref validate ./skills/<skill-name>
```
