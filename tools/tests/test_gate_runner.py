import json
from pathlib import Path
import shutil
import subprocess
from tempfile import TemporaryDirectory
import unittest


REPO_ROOT = Path(__file__).resolve().parents[2]
POWERSHELL = shutil.which('powershell.exe')


@unittest.skipUnless(POWERSHELL, 'the runner uses Windows PowerShell for script steps')
class GateRunnerSummaryTests(unittest.TestCase):
    def setUp(self):
        parent = REPO_ROOT / 'local' / 'Logs' / 'gate-runner-selftest'
        self.assertEqual(parent.resolve(), parent)
        parent.mkdir(parents=True, exist_ok=True)
        temporary = TemporaryDirectory(dir=parent)
        self.addCleanup(temporary.cleanup)
        workspace = Path(temporary.name)
        self.results = workspace / 'results'
        self.config = workspace / 'gates.json'
        script = workspace / 'fixture.ps1'
        script.write_text('param([int]$ExitCode = 0)\nexit $ExitCode\n', encoding='ascii')

        def step(name, exit_code=0):
            return {'name': name, 'kind': 'powershell-script', 'script': str(script),
                    'arguments': ['-ExitCode', str(exit_code)], 'timeoutSeconds': 15}

        self.config.write_text(json.dumps({'defaultGate': 'parent', 'gates': [
            {'name': 'parent', 'steps': [
                step('First'), {'name': 'Nested', 'kind': 'gate', 'gate': 'child'},
            ]},
            {'name': 'child', 'steps': [step('Child step')]},
            {'name': 'failing', 'steps': [step('Fails', 7), step('Not reached')]},
        ]}), encoding='utf-8')

    def run_gate(self, *arguments, gate='parent'):
        command = [POWERSHELL, '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
                   str(REPO_ROOT / 'tools' / 'run_test_gate.ps1'),
                   '-ConfigPath', str(self.config), '-ResultsDirectory', str(self.results),
                   *arguments]
        result = subprocess.run(command, cwd=REPO_ROOT, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, text=True, errors='replace', timeout=65)
        summaries = list(self.results.glob(f'*/{gate}/gate-summary.json'))
        self.assertEqual(len(summaries), 1, result.stdout)
        return result, self.read_summary(summaries[0])

    @staticmethod
    def read_summary(path):
        return json.loads(Path(path).read_text(encoding='utf-8-sig'))

    def test_default_gate_runs_all_steps(self):
        result, summary = self.run_gate()
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertIn("Gate 'parent' passed", result.stdout)
        self.assertEqual(summary['executionScope'], 'FullGate')
        self.assertIsNone(summary['selectedStepName'])
        self.assertEqual(summary['status'], 'Passed')
        self.assertEqual([step['name'] for step in summary['steps']], ['First', 'Nested'])

    def test_single_step_does_not_claim_gate_pass(self):
        result, summary = self.run_gate('-StepName', 'First')
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertIn("Step 'First' in gate 'parent' passed", result.stdout)
        self.assertNotIn("Gate 'parent' passed", result.stdout)
        self.assertEqual(summary['executionScope'], 'SingleStep')
        self.assertEqual(summary['selectedStepName'], 'First')
        self.assertEqual(summary['status'], 'Passed')
        self.assertEqual([step['name'] for step in summary['steps']], ['First'])

    def test_selected_nested_gate_runs_fully_without_marking_parent_full(self):
        result, summary = self.run_gate('-StepName', 'Nested')
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertEqual(summary['executionScope'], 'SingleStep')
        self.assertEqual([step['name'] for step in summary['steps']], ['Nested'])
        child = self.read_summary(summary['steps'][0]['summaryPath'])
        self.assertEqual(child['gatePath'], ['parent', 'child'])
        self.assertEqual(child['executionScope'], 'FullGate')
        self.assertIsNone(child['selectedStepName'])
        self.assertEqual(child['status'], 'Passed')

    def test_missing_step_preserves_requested_scope_in_failed_summary(self):
        result, summary = self.run_gate('-StepName', 'Missing')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Step 'Missing' in gate 'parent' failed", result.stdout)
        self.assertEqual(summary['executionScope'], 'SingleStep')
        self.assertEqual(summary['selectedStepName'], 'Missing')
        self.assertEqual(summary['status'], 'Failed')
        self.assertEqual(summary['steps'], [])

    def test_failed_step_does_not_change_requested_full_scope(self):
        result, summary = self.run_gate('-Gate', 'failing', gate='failing')
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(summary['executionScope'], 'FullGate')
        self.assertIsNone(summary['selectedStepName'])
        self.assertEqual(summary['status'], 'Failed')
        self.assertEqual([step['name'] for step in summary['steps']], ['Fails'])
        self.assertEqual(summary['steps'][0]['exitCode'], 7)
