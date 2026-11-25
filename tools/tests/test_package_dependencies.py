"""使用隔离 UPM 夹具验证依赖审计；不启动 Unity、不修改仓库包。"""

import json
from pathlib import Path
import shutil
import subprocess
from tempfile import TemporaryDirectory
import unittest


REPO_ROOT = Path(__file__).resolve().parents[2]
POWERSHELL = shutil.which('powershell.exe')
CONSUMER = 'com.abilitykit.consumer'
PROVIDER = 'com.abilitykit.provider'
PROVIDER_GUID = '1234567890abcdef1234567890abcdef'


@unittest.skipUnless(POWERSHELL, 'the audit runs in Windows PowerShell')
class PackageDependencyAuditTests(unittest.TestCase):
    def setUp(self):
        parent = REPO_ROOT / 'local' / 'Logs' / 'package-dependency-selftest'
        parent.mkdir(parents=True, exist_ok=True)
        temporary = TemporaryDirectory(dir=parent, prefix='upm fixture ')
        self.addCleanup(temporary.cleanup)
        self.packages = Path(temporary.name)
        self.manifest(CONSUMER)
        self.manifest(PROVIDER)
        self.assembly(PROVIDER, 'Runtime/Provider.asmdef', 'AbilityKit.Provider',
                      guid=PROVIDER_GUID)

    def write_json(self, path, data):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(data), encoding='utf-8')

    def manifest(self, name, dependencies=None):
        self.write_json(self.packages / name / 'package.json', {
            'name': name, 'version': '0.1.0', 'dependencies': dependencies or {},
        })

    def assembly(self, package, relative, name, references=(), guid=None, **fields):
        path = self.packages / package / relative
        self.write_json(path, {'name': name, 'references': list(references), **fields})
        if guid:
            Path(str(path) + '.meta').write_text(f'guid: {guid}\n', encoding='ascii')

    def audit(self, *arguments, packages=CONSUMER):
        result = subprocess.run([
            POWERSHELL, '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
            str(REPO_ROOT / 'tools' / 'audit_unity_package_dependencies.ps1'),
            '-PackagesRoot', str(self.packages), '-PackageNames', packages,
            *arguments,
        ], cwd=REPO_ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            text=True, errors='replace', timeout=30)
        return result

    def assert_passes(self, *arguments):
        result = self.audit(*arguments)
        self.assertEqual(result.returncode, 0, result.stdout)
        return result

    def assert_fails(self, message, *arguments):
        result = self.audit(*arguments)
        self.assertNotEqual(result.returncode, 0, result.stdout)
        self.assertIn(message, result.stdout)
        return result

    def test_named_and_guid_references_require_direct_dependency(self):
        for reference in ['AbilityKit.Provider', 'GUID:' + PROVIDER_GUID]:
            with self.subTest(reference=reference):
                self.manifest(CONSUMER)
                self.assembly(CONSUMER, 'Runtime/Consumer.asmdef', 'AbilityKit.Consumer',
                              [reference])
                self.assert_fails(PROVIDER)
                self.manifest(CONSUMER, {PROVIDER: '0.1.0'})
                self.assert_passes('-StrictInternalReferences')

    def test_transitive_dependency_does_not_satisfy_direct_reference(self):
        bridge = 'com.abilitykit.bridge'
        self.manifest(bridge, {PROVIDER: '0.1.0'})
        self.manifest(CONSUMER, {bridge: '0.1.0'})
        self.assembly(CONSUMER, 'Runtime/Consumer.asmdef', 'AbilityKit.Consumer',
                      ['AbilityKit.Provider'])
        self.assert_fails(PROVIDER)

    def test_editor_is_opt_in_by_directory_and_platform(self):
        for relative, fields in [
            ('Editor/Consumer.asmdef', {}),
            ('Authoring/Consumer.asmdef', {'includePlatforms': ['Editor']}),
        ]:
            with self.subTest(relative=relative):
                self.assembly(CONSUMER, relative, 'AbilityKit.Consumer.Editor',
                              ['AbilityKit.Provider'], **fields)
                self.assert_passes()
                self.assert_fails(PROVIDER, '-IncludeEditor')
                (self.packages / CONSUMER / relative).unlink()

    def test_tests_and_optional_samples_do_not_become_runtime_dependencies(self):
        for relative, fields in [
            ('Tests/Consumer.asmdef', {}),
            ('Samples/Consumer.asmdef', {}),
            ('Samples~/Consumer.asmdef', {}),
            ('Runtime/OptionalTests.asmdef', {'optionalUnityReferences': ['TestAssemblies']}),
            ('Runtime/RunnerTests.asmdef', {'references': ['UnityEngine.TestRunner']}),
        ]:
            references = ['AbilityKit.Provider', *fields.pop('references', [])]
            self.assembly(CONSUMER, relative, Path(relative).stem, references, **fields)
        self.assert_passes('-IncludeEditor', '-StrictInternalReferences')

    def test_unresolved_internal_references_warn_or_fail_explicitly(self):
        for reference in ['AbilityKit.Missing', 'com.abilitykit.missing',
                          'GUID:00000000000000000000000000000000']:
            with self.subTest(reference=reference):
                self.assembly(CONSUMER, 'Runtime/Consumer.asmdef', 'AbilityKit.Consumer',
                              [reference])
                result = self.assert_passes()
                self.assertIn('owner unknown for: ' + reference, result.stdout)
                self.assert_fails('unresolved internal assembly references',
                                  '-StrictInternalReferences')

    def test_unknown_external_assembly_remains_a_warning(self):
        self.assembly(CONSUMER, 'Runtime/Consumer.asmdef', 'AbilityKit.Consumer',
                      ['Microsoft.CSharp'])
        result = self.assert_passes('-StrictInternalReferences')
        self.assertIn('owner unknown for: Microsoft.CSharp', result.stdout)

    def test_precompiled_reference_requires_its_known_package(self):
        self.assembly(CONSUMER, 'Runtime/Consumer.asmdef', 'AbilityKit.Consumer',
                      precompiledReferences=['Newtonsoft.Json.dll'])
        self.assert_fails('com.unity.nuget.newtonsoft-json')
        self.manifest(CONSUMER, {'com.unity.nuget.newtonsoft-json': '3.2.1'})
        self.assert_passes('-StrictInternalReferences')

    def test_same_package_assemblies_and_comma_separated_selection(self):
        self.assembly(CONSUMER, 'Runtime/Consumer.asmdef', 'AbilityKit.Consumer',
                      ['AbilityKit.Consumer.Helper'])
        self.assembly(CONSUMER, 'Runtime/Helper.asmdef', 'AbilityKit.Consumer.Helper')
        result = self.audit('-StrictInternalReferences', packages=CONSUMER + ',' + PROVIDER)
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertIn('[' + PROVIDER + ']', result.stdout)


if __name__ == '__main__':
    unittest.main()
