# Public release checklist — v1.0.0-rc1

The source is prepared for a public GitHub release candidate, but publication still follows the project release pipeline.

Required before publishing the RC:

- RC build/deploy passes and build/runtime DLL SHA-256 match;
- short RC runtime check passes;
- `tools/public-source-audit.ps1` reports `PUBLIC SOURCE AUDIT: PASS`;
- MIT `LICENSE` is present;
- `dist/distribution.json` version/tag/repository match `1.0.0-rc1`;
- deterministic package is built from the verified installed runtime;
- `verify-release.ps1` reports `VERIFY OK` and `Runtime: MATCH`;
- Git staged-file audit contains no generated/private/local-machine artifacts;
- source is committed and pushed before the annotated tag is created;
- GitHub Release is created as a **pre-release** for tag `v1.0.0-rc1`;
- release ZIP and `.zip.sha256` are uploaded;
- the GitHub-downloaded ZIP SHA is re-verified.

Do not add the RC to the public stable catalog. Stable catalog inclusion belongs to the final `v1.0.0` promotion after RC field verification.
