#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

: "${COFFEESHOP_TEST_POSTGRES:?Set a disposable PostgreSQL admin connection privately.}"
iterations="${COFFEESHOP_CONCURRENCY_ITERATIONS:-3}"
[[ "$iterations" =~ ^[1-9][0-9]*$ ]] && (( iterations >= 2 )) || {
    printf 'Use at least two concurrency iterations.\n' >&2
    exit 1
}
results_dir="${COFFEESHOP_CONCURRENCY_RESULTS:-$PWD/TestResults/concurrency}"
mkdir -p "$results_dir"
results_dir="$(cd "$results_dir" && pwd)"
printf '{"passed":false,"state":"not_complete"}\n' > "$results_dir/summary.json"

for iteration in $(seq 1 "$iterations"); do
    for suite in Infrastructure Api; do
        build_args=()
        if (( iteration > 1 )); then build_args+=(--no-build --no-restore); fi
        dotnet test "tests/CoffeeShop.$suite.Tests/CoffeeShop.$suite.Tests.csproj" \
            --configuration Release "${build_args[@]}" --filter 'FullyQualifiedName~Concurrent' \
            --logger "trx;LogFileName=$suite.trx" --results-directory "$results_dir/$iteration"
    done
done

python3 - "$results_dir" "$iterations" <<'PY'
import json
import pathlib
import sys
import xml.etree.ElementTree as ET

directory = pathlib.Path(sys.argv[1])
iterations = int(sys.argv[2])
required = {
    'Confirm_ConcurrentRetries_ApplyOrderEffectsOnce',
    'Confirm_ConcurrentOrdersForSameTable_OnlyOneOrderOccupiesAndConsumes',
    'Cancel_ConcurrentRequestsFromIndependentContexts_ApplyEffectsOnce',
    'Restore_ConcurrentRetriesAndLaterCancellation_DoNotRestoreTwice',
    'ConcurrentCancellationReversals_ApplyAppendOnlyReversalOnce',
    'ConcurrentRefresh_OnlyOneSucceedsAndReuseInvalidatesSuccessor',
}
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
runs = []
for iteration in range(1, iterations + 1):
    names = set()
    for suite in ('Infrastructure', 'Api'):
        root = ET.parse(directory / str(iteration) / f'{suite}.trx').getroot()
        counts = root.find('t:ResultSummary/t:Counters', ns).attrib
        total = int(counts['total'])
        if total == 0 or any(int(counts[key]) != total for key in ('executed', 'passed')):
            raise SystemExit(f'Incomplete or failing {suite} run {iteration}; no completion claimed.')
        cases = root.findall('t:Results/t:UnitTestResult', ns)
        if len(cases) != total or any(case.attrib['outcome'] != 'Passed' for case in cases):
            raise SystemExit(f'Invalid test results in {suite} run {iteration}.')
        tests = [case.attrib['testName'] for case in cases]
        names.update(tests)
        runs.append({'iteration': iteration, 'suite': suite, 'passed': total, 'failed': 0, 'skipped': 0, 'tests': tests})
    missing = [name for name in required if not any(test.endswith(name) for test in names)]
    if missing:
        raise SystemExit(f'Required concurrency coverage missing in run {iteration}: {missing}')

result = {'passed': True, 'iterations': iterations, 'totalPassed': sum(run['passed'] for run in runs), 'runs': runs}
(directory / 'summary.json').write_text(json.dumps(result, indent=2) + '\n')
print(f"PASS: {iterations} complete concurrency iterations, {result['totalPassed']} tests, zero failures/skips.")
PY
