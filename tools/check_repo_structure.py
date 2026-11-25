"""检查共享 Markdown 链接，并报告散落的测试产物。"""

import argparse
from pathlib import Path
import re
import subprocess
from urllib.parse import unquote, urlsplit


DOC_ROOTS = (
    'README.md', 'AGENTS.md', 'Docs', 'plans', 'tools', '.claude/skills',
    'Unity/Packages/README.md', 'Unity/Packages/AGENTS.md',
)
FENCE = re.compile(r'^\s*(?:>\s*)*(`{3,}|~{3,})(.*)$')
INLINE = re.compile(r'(?<!`)(`+)(?!`).*?(?<!`)\1(?!`)')
LINK = re.compile(r'!?\[[^\[\]\n]*\]\(\s*(<[^<>]+>|[^\s()]+)(?:\s+["\'][^\n]*["\'])?\)')
DEFINITION = re.compile(r'^\s*\[[^\]]+\]:\s*(<[^<>]+>|\S+)')
TEST_RESULT = re.compile(rb'<(?:test-run|test-results|TestRun)(?=[\s/>])', re.IGNORECASE)


def documentation(root, roots=DOC_ROOTS):
    result = subprocess.run(
        ['git', '-C', str(root), 'ls-files', '-z', '--cached', '--others', '--exclude-standard', '--', *roots],
        check=True, stdout=subprocess.PIPE,
    )
    for name in sorted(set(result.stdout.decode('utf-8').split('\0'))):
        path = root / name
        if path.suffix.lower() == '.md' and path.is_file() and path.resolve().is_relative_to(root):
            yield path


def markdown_links(text):
    fence = ''
    for number, line in enumerate(text.splitlines(), 1):
        marker = FENCE.match(line)
        if marker:
            run, info = marker.groups()
            if not fence:
                fence = run
            elif run.startswith(fence) and not info.strip():
                fence = ''
            continue
        if fence:
            continue
        visible = INLINE.sub('code', line)
        definition = DEFINITION.match(visible)
        if definition:
            yield number, definition[1].strip('<>')
        for match in LINK.finditer(visible):
            yield number, match[1].strip('<>')


def link_problem(root, document, destination, package=None):
    if re.match(r'^[A-Za-z]:[/\\]', destination) or destination.lower().startswith('file:'):
        return '请使用仓库相对路径'
    try:
        parsed = urlsplit(destination)
    except ValueError:
        return '无效链接目标'
    if parsed.scheme or parsed.netloc or not parsed.path:
        return None
    raw_path = unquote(parsed.path).replace('\\', '/')
    if raw_path.startswith('/'):
        return '请使用仓库相对路径'
    resolved = (document.parent / raw_path).resolve()
    if not resolved.is_relative_to(root):
        return '目标位于仓库之外'
    if package is not None and not resolved.is_relative_to(package):
        return '目标位于包之外'
    relative = resolved.relative_to(root)
    if relative.parts and relative.parts[0].lower() in ('local', 'artifacts'):
        return '仅本地可用的证据应使用行内代码，不应作为共享链接'
    if not resolved.exists():
        return '目标不存在'
    return None


def is_test_result(data):
    return TEST_RESULT.search(data[:4096].replace(b'\0', b'')) is not None


def loose_outputs(root):
    for directory in (root, root / 'Unity'):
        if not directory.is_dir() or not directory.resolve().is_relative_to(root):
            continue
        for path in sorted(directory.iterdir()):
            if path.is_symlink() or not path.is_file():
                continue
            extension = path.suffix.lower()
            if extension not in ('.log', '.trx', '.xml'):
                continue
            if extension == '.xml':
                with path.open('rb') as stream:
                    if not is_test_result(stream.read(4096)):
                        continue
            yield path.relative_to(root).as_posix()


def staged_local_files(root):
    git = ['git', '-C', str(root)]
    names = subprocess.run(
        [*git, 'diff', '--cached', '--no-renames', '--name-only', '--diff-filter=ACMT', '-z', '--'],
        check=True, stdout=subprocess.PIPE,
    ).stdout.decode('utf-8').split('\0')
    for name in filter(None, names):
        path = Path(name)
        folders = tuple(part.lower() for part in path.parts[:-1])
        if folders[:2] in (('local', 'logs'), ('local', 'tools'), ('artifacts', 'test-gates')):
            yield name
            continue
        if {'fixtures', 'testdata'}.intersection(folders):
            continue
        extension = path.suffix.lower()
        if extension in ('.log', '.trx'):
            yield name
        elif extension == '.xml':
            data = subprocess.run([*git, 'show', f':0:{name}'], check=True, stdout=subprocess.PIPE).stdout
            if is_test_result(data):
                yield name


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--strict', action='store_true', help='链接或编码错误时失败；散落产物仍仅提醒')
    parser.add_argument('--package', action='append', default=[], metavar='NAME',
                        help='同时检查指定 AbilityKit Unity 包内的 Markdown，可重复指定')
    parser.add_argument('--staged-outputs', action='store_true',
                        help='暂存运行产物或仅供本地使用的工具时失败；不阻断未改动的历史文件')
    options = parser.parse_args(arguments)
    root = Path(__file__).resolve().parent.parent
    packages = {}
    for name in options.package:
        if not re.fullmatch(r'com\.abilitykit\.[a-z0-9_-]+(?:\.[a-z0-9_-]+)*', name):
            parser.error(f'无效的 AbilityKit 包名： {name}')
        path = root / 'Unity' / 'Packages' / name
        if path.resolve() != path or not (path / 'package.json').is_file():
            parser.error(f'不是本地 AbilityKit 包： {name}')
        packages[path.relative_to(root).as_posix()] = path

    def selected_documents():
        for document in documentation(root):
            yield document, None
        for scope, package in packages.items():
            for document in documentation(root, (scope,)):
                yield document, package

    checked = broken = encoding_errors = 0
    for document, package in selected_documents():
        try:
            text = document.read_text(encoding='utf-8-sig')
        except UnicodeDecodeError:
            print(f'ENCODING {document.relative_to(root).as_posix()}: 请检查非 UTF-8 字节')
            encoding_errors += 1
            continue
        for line, destination in markdown_links(text):
            checked += 1
            problem = link_problem(root, document, destination, package)
            if problem:
                print(f'{document.relative_to(root).as_posix()}:{line}: {problem}: {destination}')
                broken += 1
    outputs = list(loose_outputs(root))
    for path in outputs:
        print(f'OUTPUT {path}: 请核对产出方与消费方；本地产物应位于 local/Logs/')
    staged = list(staged_local_files(root)) if options.staged_outputs else []
    for path in staged:
        print(f'STAGED LOCAL FILE {path}: 请勿提交运行产物和一次性工具')
    if options.staged_outputs:
        print(f'发现 {len(staged)} 项已暂存本地文件问题。')
    print(f'已检查 {checked} 个 Markdown 链接目标；链接问题 {broken} 项，'
          f'编码问题 {encoding_errors} 项，散落产物提醒 {len(outputs)} 项。')
    return int(bool(staged) or (options.strict and (broken > 0 or encoding_errors > 0)))


if __name__ == '__main__':
    raise SystemExit(main())
