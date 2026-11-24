import argparse
import base64
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit


ROOT = Path(__file__).resolve().parents[2]
UNITY = ROOT / "Unity"
TARGET = "com.abilitykit.triggering"
WORLD_DI = "com.abilitykit.world.di"
UNSAFE = "org.nuget.system.runtime.compilerservices.unsafe"
UNSAFE_DLL = "System.Runtime.CompilerServices.Unsafe.dll"
UNSAFE_SHA256 = "52fb179fb0ec10918a3f5115c9a7290222e22886d173aab4509580824ff5be37"
FORBIDDEN = {"com.abilitykit.analyzer", "com.abilitykit.thirdparty.unsafe"}
OPENUPM = "https://package.openupm.com"
UNITY_REGISTRY = "https://packages.unity.com"
PUBLIC = {UNSAFE: OPENUPM, "com.cysharp.memorypack": OPENUPM,
          "com.unity.nuget.newtonsoft-json": UNITY_REGISTRY}
SUCCESS = "Triggering consumer passed: mainline, Timed, WorldId"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def save_json(path, value):
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def require_source_path(path):
    require(not path.is_symlink() and not path.is_junction(), f"Unsupported source link: {path}")


def collect(packages):
    versions, candidates = {}, {}

    def visit(name, version):
        require(re.fullmatch(r"[a-z0-9][a-z0-9.-]*", name), f"Invalid package name: {name}")
        require(re.fullmatch(r"\d+\.\d+\.\d+(?:-[a-zA-Z0-9.-]+)?", version),
                f"Expected exact package version: {name}@{version}")
        require(name not in FORBIDDEN, f"Forbidden dependency: {name}")
        if name in versions:
            require(versions[name] == version, f"Conflicting versions: {name}")
            return
        versions[name] = version
        if name in PUBLIC:
            return
        require(name.startswith("com.abilitykit."), f"No registry policy for {name}")
        source = packages / name
        require_source_path(source)
        require_source_path(source / "package.json")
        manifest = read_json(source / "package.json")
        require((manifest["name"], manifest["version"]) == (name, version),
                f"Candidate source does not match {name}@{version}")
        candidates[name] = (source, manifest)
        for dependency, constraint in manifest.get("dependencies", {}).items():
            visit(dependency, constraint)

    require_source_path(packages / TARGET)
    require_source_path(packages / TARGET / "package.json")
    visit(TARGET, read_json(packages / TARGET / "package.json")["version"])
    require(WORLD_DI in candidates and
            candidates[WORLD_DI][1].get("dependencies", {}).get(UNSAFE) == "5.0.0",
            "World DI must declare the Unsafe 5.0.0 provider")
    return versions, candidates


def archive_candidates(candidates, output):
    archives, hashes = {}, {}
    for name, (source, manifest) in candidates.items():
        snapshot = output / "snapshots" / name
        shutil.copytree(source, snapshot, symlinks=True)
        require(read_json(snapshot / "package.json") == manifest, f"Candidate changed during copy: {name}")
        archive = output / f"{name}.tgz"
        with tarfile.open(archive, "w:gz") as bundle:
            for path in sorted(snapshot.rglob("*")):
                require(not path.is_symlink() and not path.is_junction(), f"Unsupported package link: {path}")
                if path.is_file():
                    bundle.add(path, arcname=f"package/{path.relative_to(snapshot).as_posix()}", recursive=False)
        archives[name] = archive.read_bytes()
        hashes[name] = hashlib.sha256(archives[name]).hexdigest()
    return archives, hashes


def prepare_candidate(output, versions, candidates):
    archive_suffixes = {".tgz", ".tar", ".gz", ".zip", ".7z", ".rar", ".nupkg", ".unitypackage"}
    for source, _ in candidates.values():
        for directory, subdirectories, files in os.walk(source):
            for entry in subdirectories + files:
                require_source_path(Path(directory) / entry)
            for entry in files:
                require(Path(entry).suffix.lower() not in archive_suffixes, f"Pre-existing archive is not a candidate source: {Path(directory) / entry}")

    _, hashes = archive_candidates(candidates, output)
    report = {
        "mode": "local-source-only",
        "candidatePackages": {name: {"version": versions[name], "file": f"{name}.tgz", "sha256": digest}
                              for name, digest in hashes.items()},
        "publicDependenciesDeclaredOnly": {name: version for name, version in versions.items() if name in PUBLIC},
        "validationStatus": "prepared-only",
    }
    save_json(output / "candidate-inputs.json", report)
    print(f"Prepared {len(hashes)} local candidate archives; no consumer tests run.", flush=True)


def test_registry(candidates, archives, output):
    class Registry(BaseHTTPRequestHandler):
        def do_GET(self):
            self.respond(True)

        def do_HEAD(self):
            self.respond(False)

        def log_message(self, message, *args):
            with (output / "registry.log").open("a", encoding="utf-8") as stream:
                stream.write((message % args) + "\n")

        def respond(self, send_body):
            name, *suffix = urlsplit(self.path).path.strip("/").split("/")
            if name not in candidates:
                self.send_error(404)
                return
            manifest = candidates[name][1]
            version = manifest["version"]
            filename = f"{name}-{version}.tgz"
            archive = archives[name]
            if suffix == ["-", filename]:
                content, content_type = archive, "application/octet-stream"
            elif not suffix or suffix == [version]:
                package = {**manifest, "dist": {
                    "tarball": f"http://127.0.0.1:{self.server.server_port}/{name}/-/{filename}",
                    "shasum": hashlib.sha1(archive).hexdigest(),
                    "integrity": "sha512-" + base64.b64encode(hashlib.sha512(archive).digest()).decode(),
                }}
                metadata = {"name": name, "dist-tags": {"latest": version}, "versions": {version: package}}
                content, content_type = json.dumps(package if suffix else metadata).encode(), "application/json"
            else:
                self.send_error(404)
                return
            self.send_response(200)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(content)))
            self.end_headers()
            if send_body:
                self.wfile.write(content)

    return ThreadingHTTPServer(("127.0.0.1", 0), Registry)


def verify_install(project, output, versions, endpoint):
    root_dependencies = read_json(project / "Packages/manifest.json")["dependencies"]
    require(root_dependencies == {TARGET: versions[TARGET]}, f"Unexpected root dependencies: {root_dependencies}")
    locked = read_json(project / "Packages/packages-lock.json")["dependencies"]
    require(locked.keys() == versions.keys(), f"Unexpected resolved packages: {sorted(locked.keys() ^ versions.keys())}")
    require(locked[TARGET]["depth"] == 0 and all(locked[name]["depth"] > 0 for name in versions if name != TARGET),
            "Only Triggering may be a root dependency")
    for name, version in versions.items():
        package = locked[name]
        require((package["version"], package["source"], package["url"]) == (version, "registry", PUBLIC.get(name, endpoint)),
                f"Unexpected registry resolution for {name}: {package}")

    cache = project / "Library/PackageCache"
    probe = (project / "consumer-result.txt").read_text(encoding="utf-8-sig").splitlines()
    installed = cache / f"{TARGET}@{versions[TARGET]}"
    require(len(probe) == 2 and probe[0] == SUCCESS and Path(probe[1]).resolve() == installed.resolve(),
            f"Editor did not execute the installed Triggering package: {probe}")
    snapshot = output / "snapshots" / TARGET
    expected = {item.relative_to(snapshot): sha256(item) for item in (snapshot / "Runtime").rglob("*.cs")}
    actual = {item.relative_to(installed): sha256(item) for item in (installed / "Runtime").rglob("*.cs")}
    require(expected and actual == expected, "Installed Triggering Runtime differs from the candidate archive")

    dlls = [path for root in (cache, project / "Assets", project / "Packages")
            for path in root.rglob("*.dll") if path.name.casefold() == UNSAFE_DLL.casefold()]
    require(len(dlls) == 1 and dlls[0].is_relative_to(cache / f"{UNSAFE}@{versions[UNSAFE]}"),
            f"Expected exactly one Unsafe DLL from the external provider: {dlls}")
    digest = sha256(dlls[0])
    require(digest == UNSAFE_SHA256, f"Unsafe DLL does not match the migration baseline: {digest}")
    return {"rootDependencies": root_dependencies, "resolvedPackages": locked, "runtimeSourcesVerified": len(actual),
            "unsafeDll": dlls[0].relative_to(project).as_posix(), "unsafeDllSha256": digest, "editorProbe": probe[0]}


def main():
    parser = argparse.ArgumentParser(description="Validate Triggering from an isolated UPM consumer project.")
    parser.add_argument("--unity", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--player-backend", choices=("Mono", "IL2CPP"))
    parser.add_argument("--prepare-candidate", action="store_true")
    args = parser.parse_args()
    if args.prepare_candidate:
        if args.unity or args.player_backend:
            parser.error("--prepare-candidate excludes Unity and Player options")
    elif not args.unity:
        parser.error("Consumer mode requires --unity")
    output = args.output.resolve()
    output.mkdir(parents=True)
    print(f"Evidence: {output}", flush=True)
    versions, candidates = collect(UNITY / "Packages")
    if args.prepare_candidate:
        prepare_candidate(output, versions, candidates)
        return
    archives, hashes = archive_candidates(candidates, output)
    scratch_root = ROOT / "local/Logs"
    scratch_root.mkdir(parents=True, exist_ok=True)
    workspace = Path(tempfile.mkdtemp(prefix="tc-", dir=scratch_root))
    project = workspace / "p"
    print(f"Isolated project: {project}", flush=True)
    for directory in ("Assets", "Packages", "ProjectSettings"):
        (project / directory).mkdir(parents=True)
    fixture = Path(__file__).with_name("TriggeringConsumerProbe.cs")
    sample = output / "snapshots" / TARGET / "Samples~/FormalMainline/FormalTriggeringMainlineExample.cs"
    project_version = UNITY / "ProjectSettings/ProjectVersion.txt"
    shutil.copy2(fixture, project / "Assets")
    shutil.copy2(sample, project / "Assets")
    shutil.copy2(project_version, project / "ProjectSettings")
    save_json(output / "inputs.json", {"versions": versions, "archiveSha256": hashes, "fixtureSha256": sha256(fixture),
                                       "sampleSha256": sha256(sample), "projectVersion": project_version.read_text().strip(),
                                       "editor": str(args.unity.resolve()), "project": str(project),
                                       "playerBackend": args.player_backend})

    environment = {**os.environ, "UPM_CACHE_ROOT": str(workspace / "upm")}
    editor = [str(args.unity.resolve()), "-batchmode", "-nographics", "-quit", "-projectPath", str(project)]
    editor_options = {"cwd": project, "env": environment, "check": True, "creationflags": subprocess.CREATE_NO_WINDOW}
    with test_registry(candidates, archives, output) as registry:
        endpoint = f"http://127.0.0.1:{registry.server_port}"
        save_json(project / "Packages/manifest.json", {"dependencies": {TARGET: versions[TARGET]}, "scopedRegistries": [
            {"name": "Local AbilityKit candidate", "url": endpoint, "scopes": ["com.abilitykit"]},
            {"name": "OpenUPM", "url": OPENUPM, "scopes": sorted(name for name in versions if PUBLIC.get(name) == OPENUPM)},
        ]})
        server = threading.Thread(target=registry.serve_forever, daemon=True)
        server.start()
        try:
            subprocess.run(editor + ["-executeMethod", "TriggeringConsumerProbe.RunEditor", "-logFile", str(output / "editor.log")],
                           timeout=420, **editor_options)
            result = verify_install(project, output, versions, endpoint)
            save_json(output / "editor-report.json", result)
            if args.player_backend:
                subprocess.run(editor + ["-executeMethod", f"TriggeringConsumerProbe.Build{args.player_backend}",
                                         "-logFile", str(output / "player-build.log")],
                               timeout=900, **editor_options)
        finally:
            registry.shutdown()
            server.join()

    if args.player_backend:
        player_log = output / "player.log"
        subprocess.run([str(project / "Player/TriggeringConsumer.exe"), "-batchmode", "-nographics",
                        "-logFile", str(player_log)], cwd=project, check=True, timeout=90,
                       creationflags=subprocess.CREATE_NO_WINDOW)
        require(SUCCESS in player_log.read_text(encoding="utf-8", errors="replace").splitlines(),
                "Player did not report successful probes")
        if args.player_backend == "Mono":
            dlls = list((project / "Player").rglob(UNSAFE_DLL))
            require(len(dlls) == 1 and sha256(dlls[0]) == UNSAFE_SHA256,
                    f"Mono Player did not carry exactly one expected Unsafe DLL: {dlls}")
        result["playerProbe"] = {"platform": "Windows x64", "backend": args.player_backend, "result": SUCCESS}
    save_json(output / "consumer-report.json", result)
    print(SUCCESS, flush=True)


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, subprocess.SubprocessError) as error:
        sys.exit(f"Triggering consumer failed: {error}")
