import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REL = 'Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset'
OUTPUT = ROOT / REL
PUBLISHED = ROOT / 'Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/skill_flows.json'
RID_BASE = 8479231920601825350

def q(value):
    return json.dumps(value, ensure_ascii=True)

def build_flow(flow, new_id=None, name=None, tag_prefix=None):
    identity = flow['Id'] if new_id is None else new_id
    display = flow['Name'] if name is None else name
    phases = []
    refs = []
    for phase_index, phase in enumerate(flow['Phases']):
        rid = RID_BASE + len(result_refs) + phase_index
        phases.append(f'    - rid: {rid}')
        if phase['Type'] == 4:
            phase_id = phase.get('PhaseId', '')
            if tag_prefix and phase_id.startswith('ssx_'):
                phase_id = tag_prefix + phase_id[4:]
            trigger_ids = phase.get('RulePlan', {}).get('TriggerIds', [900101011])
            refs.extend([
                f'    - rid: {rid}',
                '      type: {class: SkillRulePlanPhaseDef, ns: AbilityKit.Ability.Impl.BattleDemo.Moba.Editor, asm: AbilityKit.Demo.Moba.Editor}',
                '      data:',
                f'        PhaseId: {phase_id}',
                f'        TriggerIds: {"".join(f"{v:02x}" for v in trigger_ids[0].to_bytes(4, "little"))}',
                '        AbortOnFailure: 1',
                '        FailReason: skill_release_failed' if phase_id.endswith('release') else '        FailReason: skill_commit_failed',
            ])
        else:
            timeline = phase['Timeline']
            phases_source = ['    - rid: ' + str(rid),
                '      type: {class: SkillTimelinePhaseDef, ns: AbilityKit.Ability.Impl.BattleDemo.Moba.Editor, asm: AbilityKit.Demo.Moba.Editor}',
                '      data:',
                f'        PhaseId: {phase.get("PhaseId", "")}',
                '        Timeline:',
                f'          DurationMs: {timeline["DurationMs"]}',
                '          Events:']
            events = timeline.get('Events', [])
            if not events:
                phases_source.append('          Events: []')
            else:
                for event in events:
                    tag = event.get('EventTag', '')
                    if tag_prefix and tag.startswith('ssx_'):
                        tag = tag_prefix + tag[4:]
                    phases_source.extend([
                        f'          - AtMs: {event.get("AtMs", 0)}',
                        f'            EffectId: {event["EffectId"]}',
                        f'            ExecuteMode: {event.get("ExecuteMode", 0)}',
                        f'            EventTag: {tag}',
                    ])
            refs.extend(phases_source)
    result_refs.extend(refs)
    output = [f'  - Id: {identity}', f'    Name: {q(display)}', '    PipelineContinuousTagTemplateId: 0', '    Phases:']
    output.extend(phases)
    return '\n'.join(output)

head = subprocess.check_output(['git', 'show', f'HEAD:{REL}'], cwd=ROOT, text=True, encoding='utf-8')
flows = json.loads(PUBLISHED.read_text(encoding='utf-8'))
hero7 = [f for f in flows if 10070000 <= f['Id'] < 10080000]
result_refs = []
data = [build_flow(f) for f in hero7]
mapping = {10070001: 10080001, 10070101: 10080101, 10070112: 10080112, 10070201: 10080201, 10070301: 10080301}
luban_names = {
    10080001: '鲁班七号-普通攻击',
    10080101: '鲁班七号-河豚手雷',
    10080112: '鲁班七号-强化扫射',
    10080201: '鲁班七号-无敌鲨嘴炮',
    10080301: '鲁班七号-空中支援',
}
for flow in hero7:
    new_id = mapping[flow['Id']]
    cloned = json.loads(json.dumps(flow))
    cloned['Id'] = new_id
    cloned['Name'] = luban_names[new_id]
    for phase in cloned['Phases']:
        if 'Timeline' in phase:
            for event in phase['Timeline'].get('Events', []):
                event['EffectId'] = mapping.get(event['EffectId'], event['EffectId'])
                event['EventTag'] = event.get('EventTag', '').replace('ssx_', 'luban_')
        if 'PhaseId' in phase:
            phase['PhaseId'] = phase['PhaseId'].replace('ssx_', 'luban_')
    data.append(build_flow(cloned, tag_prefix='luban_'))

# The airship has its own AI-cast skill and therefore needs a distinct flow.
airship = json.loads(json.dumps(next(f for f in hero7 if f['Id'] == 10070301)))
airship['Id'] = 10080401
airship['Name'] = '鲁班七号-空中支援-召唤物投弹'
for phase in airship['Phases']:
    if 'Timeline' in phase:
        for event in phase['Timeline'].get('Events', []):
            event['EffectId'] = 10080401
            event['EventTag'] = 'luban_airship_bomb'
    if 'PhaseId' in phase:
        phase['PhaseId'] = phase['PhaseId'].replace('ssx_', 'luban_airship_')
data.append(build_flow(airship, tag_prefix='luban_airship_'))

start = head.index('  legacyDataList:')
head = head[:start] + '\n'.join(data) + '\n' + head[start:]
ref_start = head.index('    RefIds:') + len('    RefIds:')
head = head[:ref_start] + '\n' + '\n'.join(result_refs) + head[ref_start:]
OUTPUT.write_text(head, encoding='utf-8')
print(f'rebuilt skill flow authoring with {len(hero7)} hero 1007 flows and {len(hero7)} hero 1008 flows')
