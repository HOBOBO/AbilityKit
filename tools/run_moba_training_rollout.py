"""Drive the local Console training pipe and record policy transitions as NDJSON."""

import argparse
import json
import queue
import subprocess
import threading
from pathlib import Path


class MobaTrainingClient:
    def __init__(self, console_dll, server_log, match_config=None, timeout=30):
        command = ["dotnet", str(console_dll.resolve()), "--training-stdio"]
        if match_config is not None:
            command.append(str(match_config.resolve()))
        self._timeout = timeout
        self._server_log = server_log
        self._responses = queue.Queue()
        with server_log.open("w", encoding="utf-8") as errors:
            self._process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                             stderr=errors, encoding="utf-8", text=True)
        self._reader = threading.Thread(target=self._read_responses, daemon=True)
        self._reader.start()

    def _read_responses(self):
        try:
            for line in self._process.stdout:
                self._responses.put(line)
        finally:
            self._responses.put(None)

    def _request(self, request):
        try:
            self._process.stdin.write(json.dumps(request, allow_nan=False) + "\n")
            self._process.stdin.flush()
            line = self._responses.get(timeout=self._timeout)
        except (OSError, queue.Empty) as error:
            raise RuntimeError(f"Console training pipe did not respond; see {self._server_log}") from error
        if line is None:
            raise RuntimeError(f"Console training pipe closed unexpectedly; see {self._server_log}")
        try:
            reply = json.loads(line)
        except ValueError as error:
            raise RuntimeError(f"Unexpected training pipe output: {line[:200]!r}") from error
        if not isinstance(reply, dict) or reply.get("version") != 1:
            raise RuntimeError("Unsupported Console training protocol version")
        if reply.get("ok") is not True:
            details = reply.get("error", {})
            raise RuntimeError(f"{details.get('code')}: {details.get('message')}")
        return reply

    def reset(self, seed, max_steps):
        return self._request({"op": "reset", "seed": seed, "maxSteps": max_steps})["observation"]

    def step(self, action):
        return self._request({"op": "step", "action": action})["observation"]

    def __enter__(self):
        return self

    def __exit__(self, error_type, error, traceback):
        try:
            if error_type is None:
                self._request({"op": "close"})
        finally:
            self._process.stdin.close()
            try:
                exit_code = self._process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self._process.kill()
                exit_code = self._process.wait()
            self._reader.join()
            self._process.stdout.close()
        if error_type is None and exit_code != 0:
            raise RuntimeError(f"Console training pipe exited with {exit_code}; see {self._server_log}")


def health_reward(before, after):
    """Net change in normalized HP advantage, from the controlled player's perspective."""
    def advantage(observation):
        player, opponent = observation["player"], observation["opponent"]
        return player["hp"] / player["maxHp"] - opponent["hp"] / opponent["maxHp"]

    return advantage(after) - advantage(before)


def baseline_action(observation, skill_slot):
    player, opponent = observation["player"], observation["opponent"]
    skill = next((item for item in player["skills"] if item["slot"] == skill_slot), None)
    if skill is None or skill["remainingCooldownMs"] > 0:
        return {}
    aim = opponent["position"]
    direction = {axis: aim[axis] - player["position"][axis] for axis in ("x", "y", "z")}
    return {"skill": {"slot": skill_slot, "phase": "press", "targetActorId": opponent["actorId"],
                      "aimPosition": aim, "aimDirection": direction}}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--console-dll", required=True, type=Path)
    parser.add_argument("--match-config", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--server-log", required=True, type=Path)
    parser.add_argument("--seeds", type=int, nargs="+", default=[101, 202, 101])
    parser.add_argument("--steps", type=int, default=150)
    parser.add_argument("--skill-slot", type=int, default=1)
    args = parser.parse_args()
    if args.steps <= 0 or args.skill_slot <= 0:
        parser.error("--steps and --skill-slot must be positive")
    if any(not -(2 ** 31) <= seed < 2 ** 31 for seed in args.seeds):
        parser.error("seeds must fit a signed 32-bit integer")
    logs = (Path(__file__).resolve().parent.parent / "local" / "Logs").resolve()
    paths = {path.resolve() for path in (args.output, args.server_log)}
    if len(paths) != 2 or any(logs not in path.parents for path in paths):
        parser.error("--output and --server-log must be distinct files under local/Logs/")
    for path in paths:
        path.parent.mkdir(parents=True, exist_ok=True)
    with MobaTrainingClient(args.console_dll, args.server_log, args.match_config) as client, \
            args.output.open("w", encoding="utf-8") as output:
        for episode, seed in enumerate(args.seeds):
            before = client.reset(seed, args.steps)
            total_reward = 0.0
            while not (before["terminated"] or before["truncated"]):
                action = baseline_action(before, args.skill_slot)
                after = client.step(action)
                reward = health_reward(before, after)
                record = {"version": 1, "episode": episode, "seed": seed, "observation": before,
                          "action": action, "reward": reward, "nextObservation": after}
                output.write(json.dumps(record, allow_nan=False, separators=(",", ":")) + "\n")
                total_reward += reward
                before = after
            print(f"episode={episode} seed={seed} steps={before['frame']} reward={total_reward:.6f} "
                  f"terminated={before['terminated']} truncated={before['truncated']}")


if __name__ == "__main__":
    main()
