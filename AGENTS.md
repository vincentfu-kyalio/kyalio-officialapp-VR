# Codex Project Instructions

## Commit Message Convention

Follow **Conventional Commits** specification for all commits.

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

- `scene` — Unity scene files
- `prefab` — Prefab assets
- `shader` — Shaders / ShaderGraph
- `xr` — XR / VR interaction
- `ui` — UI components
- `audio` — Audio assets or scripts
- `physics` — Physics-related
- `settings` — Project or render pipeline settings
- `deps` — Package / dependency changes
- `scripts` — General C# scripts

### Rules

- Subject is **lowercase**, no period at the end
- Subject max **50 characters**
- Use **imperative mood**: "add" not "added", "fix" not "fixed"
- Breaking changes: append `!` after type/scope, e.g. `feat(xr)!: ...`
- Reference issues in footer: `Closes #123`
- One logical change per commit; split unrelated changes into separate commits

### Body Guidelines

- Max 72 characters per line
- Explain **why** (root cause, motivation, context), not what (the diff shows that)
- For scene / prefab changes: describe object-level changes, not file names

```
# Bad — describes files, not intent
- Remove TextMesh Pro Examples & Extras folder
- Update App.unity

# Good — explains why
Removed TMP Examples to eliminate ~200 unused assets causing build warnings.
App scene bindings broke after Canvas_VR prefab restructure;
rebinding restores correct UIManager references.
```

### Examples

```
feat(xr): add teleportation anchor prefabs
fix(ui): correct canvas render mode for VR
chore(deps): upgrade XR Interaction Toolkit to 3.3.1
refactor(scripts): extract input handler into separate component
docs: add conventional commits guidelines
```
