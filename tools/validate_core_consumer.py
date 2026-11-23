"""从隔离快照生成本地验证包，执行 .NET 消费和真实 Unity 导入。"""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import tempfile
import time
import uuid
import xml.etree.ElementTree as ET
import zipfile

REPO = Path(__file__).resolve().parents[1]
PACKAGES = ['com.abilitykit.core', 'com.abilitykit.deterministic']
PROJECTS = ['AbilityKit.Deterministic', 'AbilityKit.Core']
DEFAULT_UNITY = Path('C:/Program Files/Unity/Hub/Editor/2022.3.62f1/Editor/Unity.exe')


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def execute(args, cwd, log, expect_success=True, timeout=600):
    print('执行：' + log.name, flush=True)
    started = time.monotonic()
    with log.open('w', encoding='utf-8') as output:
        result = subprocess.run([str(arg) for arg in args], cwd=cwd, stdout=output,
                                stderr=subprocess.STDOUT, timeout=timeout, encoding='utf-8')
    record = {'arguments': [str(arg) for arg in args], 'cwd': str(cwd), 'log': str(log),
              'exitCode': result.returncode, 'seconds': round(time.monotonic()-started, 2)}
    if expect_success and result.returncode != 0:
        raise RuntimeError(f'命令失败，见 {log}')
    return record


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def resolve_commit(ref):
    return subprocess.check_output(['git', 'rev-parse', '--verify', '--end-of-options', ref+'^{commit}'],
                                   cwd=REPO, text=True).strip()


def extract_snapshot(commit, target, paths):
    archive = subprocess.check_output(['git', 'archive', '--format=tar', commit, *paths], cwd=REPO)
    target.mkdir(parents=True)
    with tarfile.open(fileobj=io.BytesIO(archive)) as stream:
        stream.extractall(target, filter='data')


def snapshot_hashes(snapshot):
    return {path.relative_to(snapshot).as_posix():digest(path) for path in sorted(snapshot.rglob('*')) if path.is_file()}


def prepare(root, baseline_commit, candidate_commit):
    paths = [f'Unity/Packages/{name}' for name in PACKAGES]
    paths += ['src/Directory.Build.targets']
    for project in PROJECTS:
        paths += [f'src/{project}/{file}' for file in [project+'.csproj', 'PublicAPI.Shipped.txt', 'PublicAPI.Unshipped.txt']]
    local_policy = 'src/AbilityKit.Core/Directory.Build.targets'
    baseline_paths = paths + subprocess.check_output(['git','ls-tree','--name-only',baseline_commit,'--',local_policy],
                                                     cwd=REPO,text=True).splitlines()
    extract_snapshot(baseline_commit, root/'snapshots/baseline', baseline_paths)
    current = root/'snapshots/current'
    if candidate_commit:
        candidate_paths = paths + subprocess.check_output(['git','ls-tree','--name-only',candidate_commit,'--',local_policy],
                                                          cwd=REPO,text=True).splitlines()
        extract_snapshot(candidate_commit, current, candidate_paths)
    else:
        files = subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard','--',*paths,
                                         local_policy],cwd=REPO).decode('utf-8').splitlines()
        for name in files:
            path = REPO/name
            if not path.is_file():
                continue
            target = current/name
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(path,target)
    # 固定消费源码；后续替换或回退只改变制品引用，不改变调用代码。
    probe = 'tools/core-consumer/ConsumerProbe.cs'
    if candidate_commit:
        (root/'ConsumerProbe.cs').write_bytes(subprocess.check_output(['git','show',candidate_commit+':'+probe],cwd=REPO))
    else:
        shutil.copy2(REPO/probe,root/'ConsumerProbe.cs')
    analyzer = Path(os.environ['USERPROFILE'])/'.nuget/packages/microsoft.codeanalysis.publicapianalyzers/3.3.4/microsoft.codeanalysis.publicapianalyzers.3.3.4.nupkg'
    if not analyzer.exists():
        raise RuntimeError('先恢复 Core 工程，缺少本地公共 API 分析器验证依赖。')
    feed = root/'build-feed'
    feed.mkdir()
    shutil.copy2(analyzer,feed/analyzer.name)
    inputs=[]
    manifests={}
    for label in ['baseline','current']:
        snapshot = root/'snapshots'/label
        manifest = root/f'{label}-inputs.json'
        write_json(manifest,snapshot_hashes(snapshot))
        manifests[label]=digest(manifest)
        for package in PACKAGES:
            source = snapshot/'Unity/Packages'/package
            target = root/f'{package}-{label}.tgz'
            with tarfile.open(target,'w:gz') as stream:
                for path in sorted(source.rglob('*')):
                    if path.is_file():
                        stream.add(path,arcname='package/'+path.relative_to(source).as_posix(),recursive=False)
            inputs.append({'snapshot':label,'package':package,'archive':str(target),'sha256':digest(target),
                           'version':read_json(source/'package.json')['version']})
    state={'root':str(root),'head':resolve_commit('HEAD'),
           'baselineCommit':baseline_commit,'candidateCommit':candidate_commit,
           'source':'两份固定 Git 提交快照' if candidate_commit else 'baseline 为固定 Git 提交，current 为工作区快照',
           'snapshotManifests':manifests,'buildFeed':snapshot_hashes(feed),
           'dotnetSdk':subprocess.check_output(['dotnet','--version'],text=True).strip(),
           'probeSha256':digest(root/'ConsumerProbe.cs'),'validatorSha256':digest(Path(__file__)),
           'archives':inputs,'dotnet':[],'unity':[],'limitations':[
               '本地 NuGet 为验证制品，不是新增发布承诺；UPM 为本地 tarball，不验证 OpenUPM 服务。',
               'UPM 版本均为 0.1.0，仅验证同版本制品的替换与回退，不冒充正式版本升级。',
               '最小调用不替代完整契约、平台构建或跨目标数值一致性。']}
    write_json(root/'report.json',state)
    return state


def verify_inputs(root,state):
    if state['validatorSha256'] != digest(Path(__file__)):
        raise RuntimeError('验证器已变化，请重新准备快照。')
    for label, expected in state['snapshotManifests'].items():
        manifest=root/f'{label}-inputs.json'
        if digest(manifest)!=expected or snapshot_hashes(root/'snapshots'/label)!=read_json(manifest):
            raise RuntimeError(f'{label} 源码快照或清单与准备阶段不一致。')
    for entry in state['archives']:
        archive=root/f"{entry['package']}-{entry['snapshot']}.tgz"
        if digest(archive)!=entry['sha256']:
            raise RuntimeError(f'{archive.name} 与准备阶段不一致。')
    if digest(root/'ConsumerProbe.cs')!=state['probeSha256'] or snapshot_hashes(root/'build-feed')!=state['buildFeed']:
        raise RuntimeError('消费探针或离线构建依赖与准备阶段不一致。')


def dotnet_consume(root,state):
    state['dotnet']=[]
    state['builds']=[]
    state['nugetArtifacts']=[]
    attempt=root/'dotnet-attempts'/uuid.uuid4().hex
    attempt.mkdir(parents=True)
    feed=root/'consumer-feed'
    feed.mkdir(exist_ok=True)
    for label in ['baseline','current']:
        snapshot=attempt/label
        shutil.copytree(root/'snapshots'/label,snapshot)
        version='0.1.0-consumer.'+label
        for project in PROJECTS:
            csproj=snapshot/f'src/{project}/{project}.csproj'
            record=execute(['dotnet','restore',csproj,'--source',root/'build-feed','--packages',root/f'build-cache-{label}'],
                           snapshot,root/f'{label}-{project}-restore.log')
            state.setdefault('builds',[]).append(record)
            record=execute(['dotnet','pack',csproj,'--no-restore','--configuration','Release',
                            '-p:Version='+version,'--output',feed],snapshot,root/f'{label}-{project}-pack.log')
            state['builds'].append(record)
            archive=feed/f'{project}.{version}.nupkg'
            with zipfile.ZipFile(archive) as stream:
                nuspec=ET.fromstring(stream.read(next(n for n in stream.namelist() if n.endswith('.nuspec'))))
                deps=[(n.attrib['id'],n.attrib['version']) for n in nuspec.iter() if n.tag.endswith('}dependency')]
                assert not any('PublicApiAnalyzers' in name for name,_ in deps),deps
                if project=='AbilityKit.Core':
                    assert len(deps)==1 and deps[0][0]=='AbilityKit.Deterministic' and version in deps[0][1],deps
                else:
                    assert not deps,deps
            state.setdefault('nugetArtifacts',[]).append({'path':str(archive),'sha256':digest(archive),'dependencies':deps})
    consumer=root/'dotnet-consumer'
    consumer.mkdir(exist_ok=True)
    shutil.copy2(root/'ConsumerProbe.cs',consumer/'ConsumerProbe.cs')
    write_json(consumer/'source-input.json',{'sha256':digest(consumer/'ConsumerProbe.cs')})
    (consumer/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="local" value="'+str(feed)+'"/></packageSources></configuration>',encoding='utf-8')
    for stage,label in [('install','baseline'),('candidate','current'),('rollback','baseline')]:
        version='0.1.0-consumer.'+label
        (consumer/'Consumer.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup>
  <ItemGroup><PackageReference Include="AbilityKit.Core" Version="[{version}]" /></ItemGroup>
</Project>
''',encoding='utf-8')
        cache=attempt/f'consumer-cache-{stage}'
        record=execute(['dotnet','restore',consumer/'Consumer.csproj','--configfile',consumer/'NuGet.Config','--packages',cache],
                       consumer,root/f'dotnet-{stage}-restore.log')
        assets=read_json(consumer/'obj/project.assets.json')
        expected={'AbilityKit.Core/'+version,'AbilityKit.Deterministic/'+version}
        assert set(assets['libraries'])==expected,assets['libraries']
        assert all(value['type']=='package' for value in assets['libraries'].values())
        assert {Path(path).resolve() for path in assets['packageFolders']}=={cache.resolve()},assets['packageFolders']
        run=execute(['dotnet','run','--project',consumer/'Consumer.csproj','--no-restore','--configuration','Release'],
                    consumer,root/f'dotnet-{stage}-run.log')
        assert 'ABILITYKIT_CONSUMER_OK checks=8' in Path(run['log']).read_text(encoding='utf-8')
        for project in PROJECTS:
            with zipfile.ZipFile(feed/f'{project}.{version}.nupkg') as archive:
                expected_dll=archive.read(f'lib/net10.0/{project}.dll')
            assert (consumer/f'bin/Release/net10.0/{project}.dll').read_bytes()==expected_dll,project
        shutil.copy2(consumer/'obj/project.assets.json',root/f'dotnet-{stage}-assets.json')
        state['dotnet'].append({'stage':stage,'version':version,'libraries':sorted(expected),'checks':8,'restore':record,'run':run})
        write_json(root/'report.json',state)
    # 缺少内核时必须恢复失败，验证成功不是从用户全局缓存偷偷补齐。
    missing=root/'missing-dependency-feed'
    missing.mkdir(exist_ok=True)
    shutil.copy2(feed/'AbilityKit.Core.0.1.0-consumer.baseline.nupkg',missing)
    missing_consumer=root/'missing-dependency-consumer'
    missing_consumer.mkdir(exist_ok=True)
    shutil.copy2(consumer/'Consumer.csproj',missing_consumer/'Consumer.csproj')
    failure=execute(['dotnet','restore',missing_consumer/'Consumer.csproj','--source',missing,'--packages',attempt/'missing-dependency-cache','--force'],
                    missing_consumer,root/'dotnet-missing-dependency.log',expect_success=False)
    text=Path(failure['log']).read_text(encoding='utf-8')
    assert failure['exitCode']!=0 and 'NU1101' in text and 'AbilityKit.Deterministic' in text
    state['missingDependencyRejected']=failure
    write_json(root/'report.json',state)


def unity_consume(root,state,unity):
    state['unity']=[]
    if not unity.is_file():
        raise FileNotFoundError('未运行真实 Unity 验证：指定 Unity Editor 不存在。')
    project=root/'unity-consumer'
    state['unityInitialLibraryAbsent']=not (project/'Library').exists()
    for directory in ['Assets/Editor','Packages','ProjectSettings']:
        (project/directory).mkdir(parents=True,exist_ok=True)
    shutil.copy2(root/'ConsumerProbe.cs',project/'Assets/Editor/ConsumerProbe.cs')
    (project/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.62f1\n',encoding='utf-8')
    for stage,label in [('install','baseline'),('candidate','current'),('rollback','baseline')]:
        dependencies={package:'file:'+str(root/f'{package}-{label}.tgz').replace('\\','/') for package in PACKAGES}
        core_source=root/f'snapshots/{label}/Unity/Packages/com.abilitykit.core'
        legacy_adapter=(core_source/'Runtime/Unity/AbilityKit.Core.Unity.asmdef').is_file()
        web_request_module='com.unity.modules.unitywebrequest'
        if legacy_adapter:
            dependencies[web_request_module]='1.0.0'
        manifest=project/'Packages/manifest.json'
        write_json(manifest,{'dependencies':dependencies})
        result=project/'consumer-result.json'
        if result.exists():
            result.unlink()
        record=execute([unity,'-batchmode','-nographics','-quit','-projectPath',project,
                        '-executeMethod','ConsumerProbe.RunUnity','-logFile',root/f'unity-{stage}.log'],
                       project,root/f'unity-{stage}-launcher.log',timeout=900)
        if not result.exists():
            raise RuntimeError(f'Unity 没有生成本轮成功标志，见 {root}/unity-{stage}.log')
        verdict=read_json(result)
        assert verdict['checks']==8 and verdict['packageChecks']==3,verdict
        lock=read_json(project/'Packages/packages-lock.json')['dependencies']
        assert {name for name in lock if name.startswith('com.abilitykit.')}==set(PACKAGES),lock
        assert (web_request_module in lock)==legacy_adapter,lock
        for name in PACKAGES:
            assert lock[name]['source']=='local-tarball' and label in lock[name]['version'],lock[name]
        assemblies=project/'Library/ScriptAssemblies'
        for assembly in ['AbilityKit.Core','AbilityKit.Core.Editor','AbilityKit.Deterministic']:
            assert (assemblies/f'{assembly}.dll').exists(),assembly
        assert (assemblies/'AbilityKit.Core.Unity.dll').exists()==legacy_adapter
        resolved_paths=(project/'consumer-packages.txt').read_text(encoding='utf-8-sig').splitlines()
        verified_sources={}
        for package,resolved in zip(PACKAGES,resolved_paths):
            source=root/f'snapshots/{label}/Unity/Packages/{package}'
            source_files=list(source.rglob('*.cs'))
            for file in source_files:
                assert digest(file)==digest(Path(resolved)/file.relative_to(source)),file
            verified_sources[package]=len(source_files)
        shutil.copy2(project/'Packages/packages-lock.json',root/f'unity-{stage}-lock.json')
        shutil.copy2(manifest,root/f'unity-{stage}-manifest.json')
        state['unity'].append({'stage':stage,'snapshot':label,'verdict':verdict,'launch':record,
                               'packageLock':str(root/f'unity-{stage}-lock.json'),
                               'legacyUnityAdapter':legacy_adapter,'unityWebRequestResolved':web_request_module in lock,
                               'source_hashes_verified':verified_sources})
        write_json(root/'report.json',state)
    state['unityStatus']='真实 Editor 导入及最小调用三阶段通过；未构建 Player 或 Mono/IL2CPP。'
    write_json(root/'report.json',state)


def main():
    import sys
    sys.stdout.reconfigure(encoding='utf-8')
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mode',choices=['all','prepare','dotnet','unity'],default='all')
    parser.add_argument('--run-root',type=Path)
    parser.add_argument('--baseline-commit',default=None,help='基线提交或引用；默认 HEAD。')
    parser.add_argument('--candidate-commit',default=None,help='从固定提交提取候选及探针；省略则读取工作区。')
    parser.add_argument('--unity',type=Path,default=DEFAULT_UNITY)
    args=parser.parse_args()
    if args.run_root:
        if args.baseline_commit or args.candidate_commit:
            parser.error('--run-root 使用原有快照，不能同时指定提交。')
        root=args.run_root.resolve()
        state=read_json(root/'report.json')
        if 'snapshotManifests' not in state:
            parser.error('--run-root 须使用本版验证器准备的输入。')
    else:
        baseline_commit=resolve_commit(args.baseline_commit or 'HEAD')
        candidate_commit=resolve_commit(args.candidate_commit) if args.candidate_commit else None
        root=Path(tempfile.mkdtemp(prefix='AbilityKit-CoreConsumer-')).resolve()
        state=prepare(root,baseline_commit,candidate_commit)
    # 同一输入集按阶段串行更新证据，避免两种消费同时改写汇总。
    with (root/'run.lock').open('x',encoding='utf-8') as lock:
        lock.write(str(os.getpid()))
    try:
        write_json(REPO/'local/Logs/core-stabilization/core-consumer-latest.json',{'root':str(root),'report':str(root/'report.json')})
        print('隔离消费目录：'+str(root),flush=True)
        try:
            state.pop('failure',None)
            verify_inputs(root,state)
            if args.mode in ['all','dotnet']:
                dotnet_consume(root,state)
            if args.mode in ['all','unity']:
                unity_consume(root,state,args.unity)
        except Exception as error:
            state['failure']=str(error)
            write_json(root/'report.json',state)
            raise
    finally:
        (root/'run.lock').unlink()
    print('完成：'+str(root/'report.json'),flush=True)


if __name__=='__main__':
    main()
