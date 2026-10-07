"""Validated, reversible publication of generated MOBA artifacts."""

import json
import hashlib
import os
import shutil
import tempfile
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Artifact:
    destination: Path
    content: bytes | None
    owner: str


class PublicationPlan:
    def __init__(self, allowed_roots):
        self.roots = tuple(root.resolve() for root in allowed_roots)
        self.artifacts = {}

    def add(self, destination, content, owner):
        destination = Path(destination).resolve()
        if not any(destination.is_relative_to(root) for root in self.roots):
            raise ValueError(f"publication target outside owned roots: {destination}")
        key = str(destination).casefold()
        if key in self.artifacts:
            previous = self.artifacts[key]
            raise ValueError(f"duplicate publication target: {destination} ({previous.owner}, {owner})")
        if content is not None and not isinstance(content, bytes):
            raise TypeError(f"publication content must be bytes: {destination}")
        self.artifacts[key] = Artifact(destination, content, owner)

    def add_tree(self, source, destination, suffix, owner):
        for file in sorted(Path(source).rglob(f"*.{suffix}")):
            self.add(Path(destination) / file.relative_to(source), file.read_bytes(), owner)

    def remove(self, destination, owner):
        self.add(destination, None, owner)

    @staticmethod
    def _equal(path, content):
        if content is None:
            return not path.exists()
        if not path.is_file():
            return False
        existing = path.read_bytes()
        if path.suffix == ".json":
            try:
                return json.loads(existing.decode("utf-8-sig")) == json.loads(content.decode("utf-8-sig"))
            except (UnicodeDecodeError, json.JSONDecodeError):
                return False
        if path.suffix == ".cs":
            return existing.replace(b"\r\n", b"\n") == content.replace(b"\r\n", b"\n")
        return existing == content

    def differences(self):
        return [artifact for artifact in self.artifacts.values()
                if not self._equal(artifact.destination, artifact.content)]

    def check(self):
        differences = self.differences()
        if differences:
            paths = ", ".join(str(item.destination) for item in differences[:5])
            raise ValueError(f"published artifacts differ ({len(differences)}): {paths}")

    def report(self, root, sources):
        root = Path(root).resolve()
        source_rows = [{"path": str(path.resolve().relative_to(root)).replace("\\", "/"),
                        "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                       for path in sorted(set(sources))]
        outputs = [{"path": str(item.destination.relative_to(root)).replace("\\", "/"),
                    "owner": item.owner,
                    "sha256": hashlib.sha256(item.content).hexdigest() if item.content is not None else None}
                   for item in sorted(self.artifacts.values(), key=lambda entry: str(entry.destination))]
        return {"sources": source_rows, "outputs": outputs,
                "changes": [str(item.destination.relative_to(root)).replace("\\", "/")
                            for item in self.differences()]}

    @staticmethod
    def _pending_file(target, content):
        descriptor, name = tempfile.mkstemp(prefix=".moba-publish-", dir=target.parent)
        path = Path(name)
        try:
            with os.fdopen(descriptor, "wb") as stream:
                stream.write(content)
        except BaseException:
            path.unlink(missing_ok=True)
            raise
        return path

    def apply(self):
        differences = self.differences()
        if not differences:
            return 0
        with tempfile.TemporaryDirectory(prefix="moba-publish-") as temporary:
            backups = {}
            prepared = {}
            applied = []
            try:
                for index, artifact in enumerate(differences):
                    target = artifact.destination
                    if target.exists() and not target.is_file():
                        raise ValueError(f"publication target is not a file: {target}")
                    if target.exists():
                        backup = Path(temporary) / f"backup-{index}"
                        shutil.copy2(target, backup)
                        backups[target] = backup
                    if artifact.content is not None:
                        target.parent.mkdir(parents=True, exist_ok=True)
                        prepared[target] = self._pending_file(target, artifact.content)
                for artifact in differences:
                    target = artifact.destination
                    if artifact.content is None:
                        target.unlink(missing_ok=True)
                    else:
                        os.replace(prepared[target], target)
                    applied.append(target)
            except Exception as error:
                restoration_errors = []
                for target in reversed(applied):
                    try:
                        if target in backups:
                            restored = self._pending_file(target, backups[target].read_bytes())
                            try:
                                os.replace(restored, target)
                            finally:
                                restored.unlink(missing_ok=True)
                        else:
                            target.unlink(missing_ok=True)
                    except OSError as restore_error:
                        restoration_errors.append(f"{target}: {restore_error}")
                if restoration_errors:
                    raise OSError(f"publication failed: {error}; restoration failed: {restoration_errors}") from error
                raise OSError(f"publication failed and was restored: {error}") from error
            finally:
                for path in prepared.values():
                    path.unlink(missing_ok=True)
        return len(differences)
