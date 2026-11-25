from contextlib import redirect_stdout
from io import StringIO
from pathlib import Path
import subprocess
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch

from tools import check_repo_structure
from tools.check_repo_structure import link_problem, loose_outputs, markdown_links


class MarkdownLinkTests(unittest.TestCase):
    def test_skips_code_and_extracts_common_links(self):
        text = '\n'.join([
            '`[example](missing.md)`',
            '```markdown',
            '[example](missing.md)',
            '```',
            '[`source`](../file.cs#L20)',
            '[reference]: <a file.md> "title"',
            '![preview](art.png)',
        ])
        self.assertEqual(list(markdown_links(text)), [
            (5, '../file.cs#L20'), (6, 'a file.md'), (7, 'art.png'),
        ])

    def test_checks_relative_destinations_without_fetching_remote_links(self):
        with TemporaryDirectory() as workspace:
            root = Path(workspace).resolve()
            document = root / 'Docs' / 'guide.md'
            document.parent.mkdir()
            (root / 'source file.cs').touch()
            for destination in ('../source%20file.cs#L7', '#intro', 'https://example.invalid/missing.md'):
                with self.subTest(destination=destination):
                    self.assertIsNone(link_problem(root, document, destination))
            for destination in ('../missing.md', '../local/result.md', '../../outside.md', 'C:/temp/note.md', '/readme.md'):
                with self.subTest(destination=destination):
                    self.assertIsNotNone(link_problem(root, document, destination))


class LooseOutputTests(unittest.TestCase):
    def test_only_inspects_root_and_unity_root_and_keeps_non_test_xml_distinct(self):
        with TemporaryDirectory() as workspace:
            root = Path(workspace).resolve()
            (root / 'Unity' / 'Fixtures').mkdir(parents=True)
            (root / 'Unity' / 'Fixtures' / 'golden.xml').write_bytes(b'<test-run result="Passed"/>')
            (root / 'Unity' / 'run.xml').write_bytes('<?xml version="1.0"?><test-run result="Passed"/>'.encode('utf-16'))
            (root / 'manifest.xml').write_bytes(b'<manifest/>')
            (root / 'build.log').touch()
            self.assertEqual(list(loose_outputs(root)), ['build.log', 'Unity/run.xml'])

    def test_staged_check_rejects_local_files_but_leaves_fixtures_and_formal_tools_alone(self):
        with TemporaryDirectory() as workspace:
            root = Path(workspace).resolve()
            git = ['git', '-C', str(root)]
            subprocess.run([*git, 'init', '-q'], check=True)
            staged = {
                'Unity/run.xml': b'<test-run result="Passed"/>',
                'Unity/manifest.xml': b'<manifest/>',
                'src/run.trx': b'<TestRun/>',
                'src/build.log': b'build output',
                'src/Tests/Fixtures/golden.xml': b'<test-run/>',
                'src/Tests/TestData/example.trx': b'<TestRun/>',
                'local/Logs/summary.json': b'{}',
                'local/tools/probe/one_off.ps1': b'Write-Output test',
                'artifacts/test-gates/summary.json': b'{}',
                'tools/verify_project.ps1': b'Write-Output test',
            }
            for name, data in staged.items():
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
            subprocess.run([*git, 'add', '-f', '--', *staged], check=True)
            (root / 'Unity' / 'run.xml').write_bytes(b'<manifest/>')
            (root / 'Unity' / 'manifest.xml').write_bytes(b'<test-run/>')
            (root / 'unstaged.log').touch()
            (root / 'local' / 'tools' / 'probe' / 'unstaged.ps1').touch()
            self.assertEqual(list(check_repo_structure.staged_local_files(root)), [
                'Unity/run.xml', 'artifacts/test-gates/summary.json', 'local/Logs/summary.json',
                'local/tools/probe/one_off.ps1', 'src/build.log', 'src/run.trx',
            ])
            output = StringIO()
            with patch.object(check_repo_structure, '__file__', str(root / 'tools' / 'check_repo_structure.py')), \
                 redirect_stdout(output):
                self.assertEqual(check_repo_structure.main(['--strict']), 0)
                self.assertEqual(check_repo_structure.main(['--staged-outputs']), 1)
            self.assertIn('STAGED LOCAL FILE local/tools/probe/one_off.ps1:', output.getvalue())
            self.assertIn('发现 6 项已暂存本地文件问题。', output.getvalue())


class StrictModeTests(unittest.TestCase):
    def test_package_opt_in_rejects_links_to_other_packages(self):
        with TemporaryDirectory() as workspace:
            root = Path(workspace).resolve()
            scope = 'Unity/Packages/com.abilitykit.test'
            package = root / scope
            package.mkdir(parents=True)
            (package / 'package.json').touch()
            sibling = package.parent / 'com.abilitykit.other'
            sibling.mkdir()
            (sibling / 'README.md').touch()
            document = package / 'README.md'
            document.write_text('[manifest](package.json)\n[sibling](../com.abilitykit.other/README.md)\n', encoding='utf-8')
            output = StringIO()
            with patch.object(check_repo_structure, '__file__', str(root / 'tools' / 'check_repo_structure.py')), \
                 patch.object(check_repo_structure, 'documentation', side_effect=[[], [], [document]]) as list_docs, \
                 redirect_stdout(output):
                self.assertEqual(check_repo_structure.main(['--strict']), 0)
                self.assertEqual(check_repo_structure.main(['--strict', '--package', package.name]), 1)
            list_docs.assert_called_with(root, (scope,))
            self.assertIn('目标位于包之外: ../com.abilitykit.other/README.md', output.getvalue())

    def test_bad_encoding_fails_strict_but_not_inventory(self):
        with TemporaryDirectory() as workspace:
            root = Path(workspace).resolve()
            document = root / 'plans' / 'proposal.md'
            document.parent.mkdir()
            document.write_bytes(b'\xff\n')
            output = StringIO()
            with patch.object(check_repo_structure, '__file__', str(root / 'tools' / 'check_repo_structure.py')), \
                 patch.object(check_repo_structure, 'documentation', return_value=[document]) as list_docs, \
                 redirect_stdout(output):
                self.assertEqual(check_repo_structure.main([]), 0)
                self.assertEqual(check_repo_structure.main(['--strict']), 1)
            list_docs.assert_called_with(root)
            self.assertIn('ENCODING plans/proposal.md', output.getvalue())

    def test_reports_drafts_in_git_visible_plan_scope(self):
        self.assertIn('plans', check_repo_structure.DOC_ROOTS)
