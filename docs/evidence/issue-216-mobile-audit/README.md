# Staff mobile dependency audit

The November 23, 2026 audit used Node 26.5.0 and npm 11.17.0 against the
staff-mobile lockfile. The baseline was `cc03321` on `main`.

| Staff-mobile audit | Moderate | Low | High or critical |
| --- | ---: | ---: | ---: |
| Fresh baseline | 28 | 0 | 0 |
| Updated lockfile | 22 | 0 | 0 |
| Updated lockfile with `--omit=dev` | 20 | 0 | 0 |

GitHub withdrew the low-severity `deepmerge` advisory recorded in the original
issue, so it was already absent from the fresh baseline.
[GHSA-gcqx-w48m-w2h4](https://github.com/advisories/GHSA-gcqx-w48m-w2h4)

The lockfile pins Expo CLI `58.1.21` and Expo fingerprint `0.21.10`, which
remove their dependency on `glob`. It also pins the compatible TypeScript
ESLint `8.73.0` family, which resolves its `minimatch` dependency to `11.0.9`.
Each update fits the existing parent dependency range.

## Remaining exposure

- Jest, the Expo Jest preset and Jest's coverage tooling still use `glob`
  `10.5.0` or `13.0.6` for test and coverage file discovery. The affected
  behavior concerns filesystem traversal after cancellation. The patched
  `glob` releases start at `14.0.2`, outside the current parent ranges.
  [GHSA-3g3m-2fjr-6pw4](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4)
- Expo fingerprint and Jest tooling still use `minimatch` `10.2.6` or `9.0.9`
  for file pattern matching. The advisory concerns excessive work when
  processing crafted patterns. The patched releases start at `11.0.1`,
  outside the current parent ranges. The audit with `--omit=dev` still includes
  Expo fingerprint and parts of the Jest chain because Expo Router declares
  React Native Testing Library as an optional peer, which also declares Jest
  as a peer. These paths concern local project files, fingerprinting and test
  discovery. [GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249)

## Reproduction

Run from the repository root with the pinned mobile toolchain.

```sh
npm ci --prefix apps/staff-mobile
npm audit --prefix apps/staff-mobile --json
npm audit --prefix apps/staff-mobile --omit=dev --json
npm run check --prefix apps/staff-mobile
dotnet build src/CoffeeShop.Api --configuration Release
npm ci --prefix contracts/mobile-p0
npm run check --prefix contracts/mobile-p0
```

The checked-in audit JSON is the full report for the updated lockfile.
The PR's `dependencies`, `mobile-quality`, `mobile-p0-contract`, and
`mobile-android` jobs provide the hosted audit and build evidence.
[Full npm audit JSON](npm-audit.json)
