using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Impl.Moba.Move
{
    public sealed class MovePolicy
    {
        public bool TryAddTask(List<IMoveTask> tasks, IMoveTask incoming)
        {
            if (incoming == null) return false;

            for (int i = tasks.Count - 1; i >= 0; i--)
            {
                var t = tasks[i];
                if (t == null)
                {
                    tasks.RemoveAt(i);
                    continue;
                }

                if (t.Group != incoming.Group) continue;

                if (incoming.Priority >= t.Priority)
                {
                    t.Cancel();
                    tasks.RemoveAt(i);
                    continue;
                }

                return false;
            }

            tasks.Add(incoming);
            return true;
        }

        public bool ShouldApplyLocomotion(List<IMoveTask> tasks)
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                var t = tasks[i];
                if (t == null) continue;
                if (t.Group == MobaMoveGroup.Control) return false;
            }

            return true;
        }
    }
}
