"""Train and compare a small contextual policy selector against local MOBA rule AI."""

import argparse
import json
import math
from dataclasses import dataclass
from hashlib import sha256
from pathlib import Path
from statistics import mean

from run_moba_training_rollout import MobaTrainingClient, baseline_action, health_reward


POLICIES = ("scripted", "approach", "retreat", "strafe")
TRAIN_LAYOUTS = {"near": (0.0, 4.0), "far": (0.0, 8.0)}
EVAL_LAYOUTS = {"near": (2.0, 3.0), "far": (-3.0, 8.0)}
RANGE_POLICY = "approach-in-range"
OFFENSE_POLICIES = ("basic-attack", "skill-basic-attack")
KITING_POLICIES = ("kite-strafe", "kite-retreat")
KITE_RETREAT_INNER_RATIO = 0.85
EXPANDED_TRAIN_LAYOUTS = {"near": (3.0, 4.0), "far": (0.0, 12.0)}
EXPANDED_EVAL_LAYOUTS = {"near": (2.0, 5.0), "far": (-3.0, 12.0)}
EXTRA_OPPONENT_LOADOUTS = {
    3006: (30060101, (30060201, 30060301, 30060401, 30060501)),
    3007: (30070101, (30070201, 30070301, 30070401, 30070501)),
}
TRAIN_SEEDS = (101, 202, 303)
EVAL_SEEDS = (701, 802, 903)
SWEEP_WEIGHTS = (1.0, 1.5, 2.0, 4.0)
SWEEP_VALIDATION = ({"near": (1.0, 4.0), "far": (1.0, 12.0)}, (401, 502))
SWEEP_HOLDOUT = ({"near": (3.0, 3.0), "far": (3.0, 12.0)}, (1009, 1103))
OFFENSE_VALIDATION = ({"near": (4.0, 1.0), "far": (2.0, 11.0)}, (1201, 1303))
OFFENSE_HOLDOUT = ({"near": (4.0, 2.0), "far": (-2.0, 12.0)}, (1409, 1511))
KITING_VALIDATION = ({"near": (1.5, 3.0), "far": (2.0, 10.5)}, (1601, 1709))
KITING_HOLDOUT = ({"near": (2.0, 4.0), "far": (-2.0, 11.0)}, (1801, 1907))
MIN_OUTPUT_GAIN = 0.01
MAX_INCOMING_INCREASE = 0.03
SCORE_TOLERANCE = 1e-9
CONTEXT_DISTANCE_BOUNDARY = 6.0
SIDES = ("player", "opponent")
HEALTH_METRICS = ("HpLost", "NetHpLossRatio", "ObservedHpDropRatio", "ObservedHpRecoveryRatio")


@dataclass(frozen=True)
class Scenario:
    id: str
    context: str
    offset: tuple[float, float]
    opponent_hero: int


def displacement(observation):
    player, opponent = (observation[side]["position"] for side in SIDES)
    return opponent["x"] - player["x"], opponent["z"] - player["z"]


def action_for(policy, observation, skill_slot, cast_range=None, attack=None):
    action = baseline_action(observation, skill_slot)
    if policy == "scripted":
        return action
    offset_x, offset_z = displacement(observation)
    distance = math.hypot(offset_x, offset_z)
    if policy in (*OFFENSE_POLICIES, *KITING_POLICIES):
        attack_slot, attack_range = attack
        if policy == "basic-attack" or distance > cast_range:
            action = {}
        if not action and distance <= attack_range:
            action = baseline_action(observation, attack_slot)
        if policy in KITING_POLICIES and action:
            move_x, move_z = 0.0, 0.0
        elif distance > attack_range:
            move_x, move_z = offset_x / distance, offset_z / distance
        elif policy == "kite-strafe" and distance:
            move_x, move_z = -offset_z / distance, offset_x / distance
        elif policy == "kite-retreat" and 0.0 < distance < attack_range * KITE_RETREAT_INNER_RATIO:
            move_x, move_z = -offset_x / distance, -offset_z / distance
        else:
            move_x, move_z = 0.0, 0.0
        return {**action, "move": {"x": move_x, "z": move_z}}
    if policy == RANGE_POLICY:
        if distance > cast_range:
            action = {}
        policy = "approach"
    move_x, move_z = (offset_x / distance, offset_z / distance) if distance else (0.0, 0.0)
    if policy == "approach" and distance <= 3.0:
        move_x, move_z = 0.0, 0.0
    elif policy == "retreat":
        move_x, move_z = -move_x, -move_z
    elif policy == "strafe":
        move_x, move_z = -move_z, move_x
    return {**action, "move": {"x": move_x, "z": move_z}}


def play(client, policy, seed, steps, skill_slot, context, *, scenario=None, cast_skill=None, basic_attack=None):
    initial = current = client.reset(seed, steps)
    if not initial["opponent"]["hasBrain"] or initial["player"]["hasBrain"]:
        raise RuntimeError("The experiment needs a controlled player and a rule-AI opponent")
    distance = math.hypot(*displacement(initial))
    observed_context = "near" if distance < CONTEXT_DISTANCE_BOUNDARY else "far"
    if observed_context != context:
        raise RuntimeError(f"Expected {context} spawn context, got {observed_context} (distance {distance:g})")
    if isinstance(policy, dict):
        policy = policy[observed_context]
    diagnostics = {}
    cast_range = None
    attack = None
    if scenario is not None:
        skill_id, cast_range = cast_skill
        if not any(skill["slot"] == skill_slot and skill["skillId"] == skill_id
                   for skill in initial["player"]["skills"]):
            raise RuntimeError(f"Expected skill {skill_id} in slot {skill_slot} for {scenario.id}")
        diagnostics = {"scenarioId": scenario.id, "opponentHeroId": scenario.opponent_hero,
                       "initialDistance": distance, "skillSubmissions": 0,
                       "outOfRangeSkillSubmissions": 0, "firstSkillSubmitFrame": None,
                       "firstSkillSubmitDistance": None}
    if basic_attack is not None:
        attack_id, attack_range = basic_attack
        slots = [skill["slot"] for skill in initial["player"]["skills"] if skill["skillId"] == attack_id]
        if len(slots) != 1 or slots[0] == skill_slot:
            raise RuntimeError(f"Expected one separate basic attack slot for {attack_id} in {scenario.id}")
        attack = slots[0], attack_range
        diagnostics.update(basicAttackSlot=slots[0], basicAttackSubmissions=0, outOfRangeBasicAttackSubmissions=0)
        if policy in KITING_POLICIES:
            diagnostics["inRangeKitingMoveFrames"] = 0
    drops = dict.fromkeys(SIDES, 0.0)
    recoveries = dict.fromkeys(SIDES, 0.0)
    while not (current["terminated"] or current["truncated"]):
        action = action_for(policy, current, skill_slot, cast_range, attack)
        if ("inRangeKitingMoveFrames" in diagnostics and "skill" not in action
                and any(action["move"].values()) and math.hypot(*displacement(current)) <= attack[1]):
            diagnostics["inRangeKitingMoveFrames"] += 1
        if diagnostics and "skill" in action:
            submit_distance = math.hypot(*displacement(current))
            if diagnostics["skillSubmissions"] == 0:
                diagnostics["firstSkillSubmitFrame"] = current["frame"] + 1
                diagnostics["firstSkillSubmitDistance"] = submit_distance
            diagnostics["skillSubmissions"] += 1
            if attack is not None and action["skill"]["slot"] == attack[0]:
                diagnostics["basicAttackSubmissions"] += 1
                diagnostics["outOfRangeBasicAttackSubmissions"] += submit_distance > attack[1]
            else:
                diagnostics["outOfRangeSkillSubmissions"] += submit_distance > cast_range
        try:
            following = client.step(action)
        except RuntimeError as error:
            scenario_id = scenario.id if scenario is not None else context
            raise RuntimeError(f"{scenario_id}, seed {seed}, {policy}, step {current['frame'] + 1}: {error}") from error
        for side in SIDES:
            drop = current[side]["hp"] - following[side]["hp"]
            drops[side] += max(0.0, drop)
            recoveries[side] += max(0.0, -drop)
        current = following
    health = {}
    for side in SIDES:
        before, after = initial[side], current[side]
        health[f"{side}HpLost"] = before["hp"] - after["hp"]
        health[f"{side}NetHpLossRatio"] = before["hp"] / before["maxHp"] - after["hp"] / after["maxHp"]
        health[f"{side}ObservedHpDropRatio"] = drops[side] / before["maxHp"]
        health[f"{side}ObservedHpRecoveryRatio"] = recoveries[side] / before["maxHp"]
    return {"context": context, "seed": seed, "policy": policy, "steps": current["frame"],
            "reward": health_reward(initial, current), "terminated": current["terminated"],
            "truncated": current["truncated"], **health, **diagnostics}


def select_policy(rounds, skill_slot, steps, candidates=POLICIES):
    estimates = {context: {policy: mean(row["reward"] for row in rounds
                                        if row["context"] == context and row["policy"] == policy)
                           for policy in candidates} for context in TRAIN_LAYOUTS}
    return {"version": 1, "method": "contextual-mean-return", "skillSlot": skill_slot, "steps": steps,
            "contextDistanceBoundary": CONTEXT_DISTANCE_BOUNDARY, "trainSeeds": TRAIN_SEEDS, "estimates": estimates,
            "policies": {context: max(candidates, key=values.__getitem__) for context, values in estimates.items()}}


def evaluate_summary(rows, strategies=("random", "scripted", "trained")):
    results = {name: [row for row in rows if row["strategy"] == name]
               for name in strategies}
    return {name: {"episodes": len(episodes), "meanReward": mean(row["reward"] for row in episodes),
                   **{f"mean{side.title()}{metric}": mean(row[f"{side}{metric}"] for row in episodes)
                      for side in SIDES for metric in HEALTH_METRICS}}
            for name, episodes in results.items()}


def summarize_improvement(deltas):
    tolerance = SCORE_TOLERANCE
    return {"meanDelta": mean(deltas), "improved": sum(delta > tolerance for delta in deltas),
            "unchanged": sum(abs(delta) <= tolerance for delta in deltas),
            "worsened": sum(delta < -tolerance for delta in deltas)}


def compare_paired(rows, reference="scripted", contender="trained"):
    paired = {strategy: {} for strategy in (reference, contender)}
    for row in rows:
        strategy = row["strategy"]
        if strategy not in paired:
            continue
        key = row.get("scenarioId", row["context"]), row["seed"]
        if key in paired[strategy]:
            raise ValueError(f"Duplicate {strategy} evaluation episode: {key}")
        paired[strategy][key] = row
    before, after = paired[reference], paired[contender]
    if not before or before.keys() != after.keys():
        raise ValueError(f"Expected matched {reference} and {contender} evaluation episodes")
    return {"pairs": len(before),
            "meanRewardDelta": mean(after[key]["reward"] - before[key]["reward"] for key in before),
            "opponentObservedHpDropIncrease": summarize_improvement([
                after[key]["opponentObservedHpDropRatio"] - before[key]["opponentObservedHpDropRatio"]
                for key in before]),
            "playerObservedHpDropReduction": summarize_improvement([
                before[key]["playerObservedHpDropRatio"] - after[key]["playerObservedHpDropRatio"]
                for key in before])}


def write_json(path, data):
    path.write_text(json.dumps(data, indent=2, allow_nan=False) + "\n", encoding="utf-8")


def match_players(config):
    player = next(item for item in config["players"] if item["playerId"] == config["playerId"])
    opponent = next(item for item in config["players"] if item["playerId"] != config["playerId"])
    return player, opponent


def create_match(base_config, path, scenario):
    config = json.loads(base_config)
    player, opponent = match_players(config)
    opponent["positionX"] = player["positionX"] + scenario.offset[0]
    opponent["positionZ"] = player["positionZ"] + scenario.offset[1]
    if scenario.opponent_hero != opponent["heroId"]:
        attack, skills = EXTRA_OPPONENT_LOADOUTS[scenario.opponent_hero]
        opponent.update(heroId=scenario.opponent_hero, attributeTemplateId=scenario.opponent_hero,
                        basicAttackSkillId=attack, skillIds=skills)
    write_json(path, config)


def scenarios(layouts, extra_layouts, opponents, expanded):
    if not expanded:
        return [Scenario(context, context, offset, opponents[0]) for context, offset in layouts.items()]
    return [Scenario(f"{context}-{variant}-vs-{hero}", context, layout[context], hero)
            for context in layouts for variant, layout in enumerate((layouts, extra_layouts), start=1)
            for hero in opponents]


def resolve_cast_skill(skill_table, player, slot):
    if slot > len(player["skillIds"]):
        raise ValueError(f"Skill slot {slot} does not exist in the training player's loadout")
    return resolve_skill_range(skill_table, player["skillIds"][slot - 1])


def resolve_skill_range(skill_table, skill_id, *, normal_attack=False):
    skills = json.loads(skill_table.read_text(encoding="utf-8-sig"))
    config = next((skill for skill in skills if skill["Id"] == skill_id), {})
    if normal_attack and config.get("SkillType") != 1:
        raise ValueError(f"Skill {skill_id} is not a configured normal attack in {skill_table}")
    cast_range = config.get("Range")
    if (isinstance(cast_range, bool) or not isinstance(cast_range, (int, float))
            or not math.isfinite(cast_range) or cast_range <= 0):
        raise ValueError(f"Skill {skill_id} needs a finite positive Range in {skill_table}")
    return skill_id, cast_range


def evaluate_report(rows, strategies, expanded):
    def group(field):
        groups = {}
        for row in rows:
            groups.setdefault(row[field], []).append(row)
        return groups

    by_context = group("context")
    report = {"metrics": evaluate_summary(rows, strategies),
              "metricsByContext": {context: evaluate_summary(group_rows, strategies)
                                   for context, group_rows in by_context.items()},
              "pairedVsScripted": {"overall": compare_paired(rows),
                                   "byContext": {context: compare_paired(group_rows)
                                                 for context, group_rows in by_context.items()}}}
    if expanded:
        by_opponent = group("opponentHeroId")
        by_scenario = group("scenarioId")
        report["metricsByOpponent"] = {hero: evaluate_summary(group_rows, strategies)
                                       for hero, group_rows in by_opponent.items()}
        report["pairedVsScripted"]["byOpponent"] = {hero: compare_paired(group_rows)
                                                    for hero, group_rows in by_opponent.items()}
        report["castTimingAblation"] = {
            "reference": "approach", "contender": RANGE_POLICY,
            "overall": compare_paired(rows, "approach", RANGE_POLICY),
            "byScenario": {scenario_id: compare_paired(group_rows, "approach", RANGE_POLICY)
                           for scenario_id, group_rows in by_scenario.items()}}
    return report


def read_sweep_training(run, expected_scenarios, candidates, skill_slot, cast_skill, steps):
    model = json.loads((run / "policy.json").read_text(encoding="utf-8"))
    expected_metadata = {"version": 2, "suite": "expanded", "steps": steps, "skillSlot": skill_slot,
                         "castTimingSkillId": cast_skill[0], "castTimingRange": cast_skill[1],
                         "contextDistanceBoundary": CONTEXT_DISTANCE_BOUNDARY, "trainSeeds": list(TRAIN_SEEDS)}
    if any(model.get(key) != value for key, value in expected_metadata.items()):
        raise ValueError("Expected an expanded training run with matching steps, skill and context")
    raw = (run / "training.json").read_bytes()
    rows = json.loads(raw)
    expected = {(case.id, case.context, case.opponent_hero, seed, policy)
                for case in expected_scenarios for seed in TRAIN_SEEDS for policy in candidates}
    for row in rows:
        key = tuple(row[field] for field in ("scenarioId", "context", "opponentHeroId", "seed", "policy"))
        if key not in expected:
            raise ValueError(f"Unexpected or duplicate training episode: {key}")
        expected.remove(key)
        if not math.isclose(row["reward"], row["opponentNetHpLossRatio"] - row["playerNetHpLossRatio"],
                            abs_tol=SCORE_TOLERANCE) or not math.isfinite(row["reward"]):
            raise ValueError(f"Invalid normalized HP reward for training episode: {key}")
    if expected:
        raise ValueError(f"Missing {len(expected)} training episodes")
    if select_policy(rows, skill_slot, steps, candidates)["policies"] != model["policies"]:
        raise ValueError("Equal-weight replay does not reproduce the expanded policy")
    return rows, sha256(raw).hexdigest()


def sweep_models(rows, skill_slot, steps, candidates):
    reference = select_policy(rows, skill_slot, steps, candidates)
    models = [{"outputWeight": 1.0, "policies": reference["policies"], "estimates": reference["estimates"]}]
    for weight in SWEEP_WEIGHTS[1:]:
        rescored = [{**row, "reward": row["reward"] + (weight - 1) * row["opponentNetHpLossRatio"]}
                    for row in rows]
        model = select_policy(rescored, skill_slot, steps, candidates)
        for context, scores in model["estimates"].items():
            baseline = reference["policies"][context]
            if max(scores.values()) - scores[baseline] <= SCORE_TOLERANCE:
                model["policies"][context] = baseline
        models.append({"outputWeight": weight, "policies": model["policies"], "estimates": model["estimates"]})
    return models


def evaluate_sweep_models(rows, reference, contender):
    paired_rows = [{**row, "strategy": name} for name, model in
                   (("baseline", reference), ("weighted", contender)) for row in rows
                   if row["policy"] == model["policies"][row["context"]]]
    by_scenario = {}
    for row in paired_rows:
        by_scenario.setdefault(row["scenarioId"], []).append(row)
    overall = compare_paired(paired_rows, "baseline", "weighted")
    scenarios = {scenario_id: compare_paired(group, "baseline", "weighted")
                 for scenario_id, group in by_scenario.items()}
    passes = (overall["opponentObservedHpDropIncrease"]["meanDelta"] >= MIN_OUTPUT_GAIN - SCORE_TOLERANCE
              and all(group["playerObservedHpDropReduction"]["meanDelta"] >= -MAX_INCOMING_INCREASE - SCORE_TOLERANCE
                      for group in scenarios.values()))
    return {"outputWeight": contender["outputWeight"], "overall": overall, "byScenario": scenarios,
            "passesCriteria": passes}


def sample_sweep_cases(args, output, base_config, cast_skill, cases, seeds, policies_by_context,
                       basic_attack=None, filename_prefix=""):
    rows = []
    for case in cases:
        filename = f"{filename_prefix}{case.id}"
        match = output / f"{filename}.json"
        create_match(base_config, match, case)
        with MobaTrainingClient(args.console_dll, output / f"{filename}.log", match) as client:
            for seed in seeds:
                for policy in policies_by_context[case.context]:
                    rows.append(play(client, policy, seed, args.steps, args.skill_slot, case.context,
                                     scenario=case, cast_skill=cast_skill, basic_attack=basic_attack))
    return rows


def sample_sweep_split(args, output, base_config, cast_skill, split, split_design, opponents, models,
                       basic_attack=None):
    layouts, seeds = split_design
    cases = [Scenario(f"{split}-{context}-vs-{hero}", context, offset, hero)
             for context, offset in layouts.items() for hero in opponents]
    policies = {context: dict.fromkeys(model["policies"][context] for model in models) for context in layouts}
    rows = sample_sweep_cases(args, output, base_config, cast_skill, cases, seeds, policies, basic_attack)
    write_json(output / f"{split}-rollouts.json", rows)
    return rows


def run_reward_sweep(args, output, base_config, opponents, cast_skill, training, digest, candidates,
                     training_scenarios, basic_attack):
    validation_design, holdout_design = SWEEP_VALIDATION, SWEEP_HOLDOUT
    offense_report = {}
    if basic_attack is not None:
        supplemental_policies = (*OFFENSE_POLICIES, *KITING_POLICIES) if args.include_kiting else OFFENSE_POLICIES
        policies = dict.fromkeys(TRAIN_LAYOUTS, supplemental_policies)
        supplemental = sample_sweep_cases(args, output, base_config, cast_skill, training_scenarios,
                                          TRAIN_SEEDS, policies, basic_attack, filename_prefix="train-offense-")
        supplemental_path = output / "offense-training.json"
        write_json(supplemental_path, supplemental)
        training = [*training, *supplemental]
        candidates = (*candidates, *supplemental_policies)
        offense_report = {"basicAttackSkillId": basic_attack[0], "basicAttackRange": basic_attack[1],
                          "supplementalTrainingRows": len(supplemental),
                          "supplementalTrainingSha256": sha256(supplemental_path.read_bytes()).hexdigest(),
                          "trainingMetricsByContext": {
                              context: evaluate_summary([{**row, "strategy": row["policy"]}
                                                         for row in training if row["context"] == context], candidates)
                              for context in TRAIN_LAYOUTS}}
        validation_design, holdout_design = OFFENSE_VALIDATION, OFFENSE_HOLDOUT
    if args.include_kiting:
        validation_design, holdout_design = KITING_VALIDATION, KITING_HOLDOUT
        ablation_policies = ("skill-basic-attack", *KITING_POLICIES)
        ablation_models = [{"policies": dict.fromkeys(TRAIN_LAYOUTS, policy)} for policy in ablation_policies]
        offense_report["kiteRetreatInnerRangeRatio"] = KITE_RETREAT_INNER_RATIO
    models = sweep_models(training, args.skill_slot, args.steps, candidates)
    reference = models[0]
    validation_models = [*models, *ablation_models] if args.include_kiting else models
    validation = sample_sweep_split(args, output, base_config, cast_skill, "validation", validation_design,
                                    opponents, validation_models, basic_attack)
    if args.include_kiting:
        ablation_rows = [{**row, "strategy": row["policy"]} for row in validation]
        offense_report["kitingVsStationary"] = {policy: compare_paired(ablation_rows, "skill-basic-attack", policy)
                                                for policy in KITING_POLICIES}
    validation_results = [evaluate_sweep_models(validation, reference, model) for model in models]
    winner = max((index for index, result in enumerate(validation_results) if result["passesCriteria"]),
                 key=lambda index: (validation_results[index]["overall"]["opponentObservedHpDropIncrease"]["meanDelta"],
                                    -models[index]["outputWeight"]), default=0)
    selected = models[winner]
    selection = {"baseline": reference, "selected": selected, "variants": models,
                 "criteria": {"minOverallOpponentHpDropGain": MIN_OUTPUT_GAIN,
                              "maxPlayerHpDropIncreasePerScenario": MAX_INCOMING_INCREASE},
                 "validationResults": validation_results, "validationSeeds": validation_design[1]}
    write_json(output / "selection.json", selection)

    holdout = sample_sweep_split(args, output, base_config, cast_skill, "holdout", holdout_design,
                                 (*opponents, 3007), (reference, selected), basic_attack)
    decision = evaluate_sweep_models(holdout, reference, selected)
    report = {"version": 1, "trainingSource": str(args.training_run.resolve()), "trainingSha256": digest,
              "trainingRows": len(training), "skillSlot": args.skill_slot, "steps": args.steps,
              **selection, "holdoutSeeds": holdout_design[1], "holdoutResult": decision,
              "validationRollouts": len(validation), "holdoutRollouts": len(holdout),
              "distinctPolicyMappings": len({tuple(model["policies"].items()) for model in models}), **offense_report}
    write_json(output / "reward-sweep.json", report)
    print(json.dumps({"selectedWeight": selected["outputWeight"], "holdoutResult": decision},
                     indent=2, allow_nan=False))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--console-dll", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--steps", type=int, default=150)
    parser.add_argument("--skill-slot", type=int, default=1)
    parser.add_argument("--suite", choices=("classic", "expanded", "reward-sweep"), default="classic")
    parser.add_argument("--training-run", type=Path, help="Existing expanded run directory for reward-sweep")
    parser.add_argument("--include-offense", action="store_true", help="Sample basic-attack candidates for reward-sweep")
    parser.add_argument("--include-kiting", action="store_true", help="Add attack-and-move candidates to --include-offense")
    args = parser.parse_args()
    if args.steps <= 0 or args.skill_slot <= 0:
        parser.error("--steps and --skill-slot must be positive")
    sweeping = args.suite == "reward-sweep"
    if sweeping != (args.training_run is not None):
        parser.error("--training-run is required for reward-sweep and not used by other suites")
    if args.include_offense and not sweeping:
        parser.error("--include-offense is only supported by reward-sweep")
    if args.include_kiting and not args.include_offense:
        parser.error("--include-kiting requires --include-offense")
    log_root = (Path(__file__).resolve().parent.parent / "local" / "Logs").resolve()
    output = args.output_dir.resolve()
    if log_root not in output.parents or output.exists():
        parser.error("--output-dir must be a new directory under local/Logs/")
    config_root = args.console_dll.resolve().parent / "Configs"
    base_match = config_root / "training" / "duel.json"
    base_config = base_match.read_text(encoding="utf-8")
    player, opponent = match_players(json.loads(base_config))
    expanded = args.suite != "classic"
    candidates = (*POLICIES, RANGE_POLICY) if expanded else POLICIES
    strategies = ("random", *candidates, "trained") if expanded else ("random", "scripted", "trained")
    training_opponents = (opponent["heroId"], 3006)
    training_scenarios = scenarios(TRAIN_LAYOUTS, EXPANDED_TRAIN_LAYOUTS, training_opponents, expanded)
    eval_scenarios = scenarios(EVAL_LAYOUTS, EXPANDED_EVAL_LAYOUTS, (*training_opponents, 3007), expanded)
    cast_skill = None
    basic_attack = None
    if expanded:
        try:
            skill_table = config_root / "luban" / "moba" / "skills.json"
            cast_skill = resolve_cast_skill(skill_table, player, args.skill_slot)
            if args.include_offense:
                basic_attack = resolve_skill_range(skill_table, player["basicAttackSkillId"], normal_attack=True)
        except (OSError, ValueError, KeyError) as exc:
            parser.error(str(exc))
    if sweeping:
        try:
            training, digest = read_sweep_training(args.training_run, training_scenarios, candidates,
                                                   args.skill_slot, cast_skill, args.steps)
        except (OSError, ValueError, KeyError, TypeError) as exc:
            parser.error(f"Invalid --training-run: {exc}")
        output.mkdir(parents=True)
        run_reward_sweep(args, output, base_config, training_opponents, cast_skill, training, digest, candidates,
                         training_scenarios, basic_attack)
        return
    output.mkdir(parents=True)

    def play_scenario(client, policy, seed, scenario):
        return play(client, policy, seed, args.steps, args.skill_slot, scenario.context,
                    scenario=scenario if expanded else None, cast_skill=cast_skill)

    training = []
    for scenario in training_scenarios:
        match = output / f"train-{scenario.id}.json"
        create_match(base_config, match, scenario)
        with MobaTrainingClient(args.console_dll, output / f"train-{scenario.id}.log", match) as client:
            for seed in TRAIN_SEEDS:
                for policy in candidates:
                    training.append(play_scenario(client, policy, seed, scenario))
    model = select_policy(training, args.skill_slot, args.steps, candidates)
    metadata = {}
    if expanded:
        metadata = {"suite": args.suite, "castTimingSkillId": cast_skill[0], "castTimingRange": cast_skill[1],
                    "randomBaselinePolicies": POLICIES}
        model.update(metadata)
        model["version"] = 2
    write_json(output / "policy.json", model)
    write_json(output / "training.json", training)

    evaluation = []
    for scenario in eval_scenarios:
        match = output / f"eval-{scenario.id}.json"
        create_match(base_config, match, scenario)
        with MobaTrainingClient(args.console_dll, output / f"eval-{scenario.id}.log", match) as client:
            for seed in EVAL_SEEDS:
                for policy in candidates:
                    result = play_scenario(client, policy, seed, scenario)
                    if policy in POLICIES:
                        evaluation.append({"strategy": "random", **result})
                    if expanded or policy == "scripted":
                        evaluation.append({"strategy": policy, **result})
                evaluation.append({"strategy": "trained", **play_scenario(client, model["policies"], seed, scenario)})
    report = evaluate_report(evaluation, strategies, expanded)
    write_json(output / "evaluation.json", {"version": 3 if expanded else 2, "evalSeeds": EVAL_SEEDS,
                                            **metadata, **report,
                                            "episodes": evaluation})
    print(json.dumps({"policies": model["policies"], **report}, indent=2, allow_nan=False))


if __name__ == "__main__":
    main()
