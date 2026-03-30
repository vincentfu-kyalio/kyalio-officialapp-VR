# Contributing Guidelines

## Commit Message Convention

This project follows the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) specification.

### Format

```
<type>(<scope>): <subject>

[optional body]

[optional footer]
```

### Types

| Type | Usage |
|------|-------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Formatting, no logic change |
| `refactor` | Code restructure, no feature/fix |
| `perf` | Performance improvement |
| `test` | Adding or updating tests |
| `chore` | Build process, assets, dependencies |
| `ci` | CI/CD configuration |
| `revert` | Revert a previous commit |

### Scopes (Unity-specific)

Use scopes to indicate the area of change:

| Scope | Area |
|-------|------|
| `scene` | Unity scene files |
| `prefab` | Prefab assets |
| `shader` | Shaders / ShaderGraph |
| `xr` | XR / VR interaction |
| `ui` | UI components |
| `audio` | Audio assets or scripts |
| `physics` | Physics-related |
| `settings` | Project or render pipeline settings |
| `deps` | Package / dependency changes |
| `scripts` | General C# scripts |

### Rules

- Subject is **lowercase**, no period at the end
- Subject max **72 characters**
- Use **imperative mood**: "add" not "added", "fix" not "fixed"
- Breaking changes: append `!` after type/scope, e.g. `feat(xr)!: ...`
- Reference issues in footer: `Closes #123`

### Examples

```
feat(xr): add teleportation anchor prefabs
fix(ui): correct canvas render mode for VR
chore(deps): upgrade XR Interaction Toolkit to 3.3.1
refactor(scripts): extract input handler into separate component
docs: add conventional commits guidelines
```

### Multi-line Example

```
feat(xr): add climb and teleport interaction system

Implemented XR Interaction Toolkit climb interactables and
teleport anchors with visual affordance feedback.

Closes #42
```

## Branch Naming

```
<type>/<short-description>
```

Examples:
- `feat/xr-teleport-system`
- `fix/ui-canvas-render-mode`
- `chore/upgrade-xrit`

## Pull Requests

- Title follows the same Conventional Commits format
- Link related issues in the PR description
- Ensure the project compiles before opening a PR
