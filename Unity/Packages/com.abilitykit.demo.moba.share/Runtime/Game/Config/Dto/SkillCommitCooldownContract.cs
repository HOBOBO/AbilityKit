using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
    public static class SkillCommitCooldownContract
    {
        public static void Validate(SkillFlowDTO flow)
        {
            if (!flow.UseCommitCooldownFactor) return;
            var reservations = 0;
            var commits = 0;
            Inspect(flow.Phases, ref reservations, ref commits);
            if (reservations != 1 || commits != 1)
                throw new ArgumentException($"Flow {flow.Id}: commit cooldown factor requires exactly one resolved cooldown reservation and an explicit commit point (reservations={reservations}, commits={commits}).");
        }

        private static void Inspect(SkillPhaseDTO[] phases, ref int reservations, ref int commits)
        {
            foreach (var phase in phases ?? Array.Empty<SkillPhaseDTO>())
            {
                if (phase == null) continue;
                if (phase.Type == (int)SkillPhaseType.Economy && phase.Economy != null &&
                    phase.Economy.Operation == (int)SkillEconomyOperation.ReserveCast)
                {
                    if (!phase.Economy.StartSkillCooldown || !phase.Economy.UseResolvedSkillCooldown ||
                        !phase.Economy.RequireExplicitCommit)
                        throw new ArgumentException("Commit cooldown factor requires an explicit resolved skill cooldown reservation.");
                    reservations++;
                }
                if (phase.Type == (int)SkillPhaseType.CommitPoint)
                {
                    if (reservations != 1 || phase.CommitPoint?.RequireEconomyReservation != true)
                        throw new ArgumentException("Commit cooldown factor requires a later required economy commit point.");
                    commits++;
                }
                if (phase.Type == (int)SkillPhaseType.Sequence)
                    Inspect(phase.Children, ref reservations, ref commits);
                else
                {
                    var nestedReservations = 0;
                    var nestedCommits = 0;
                    Inspect(phase.Children, ref nestedReservations, ref nestedCommits);
                    if (phase.Repeat?.Phase != null)
                        Inspect(new[] { phase.Repeat.Phase }, ref nestedReservations, ref nestedCommits);
                    if (nestedReservations != 0 || nestedCommits != 0)
                        throw new ArgumentException("Commit cooldown reservation and commit must be on the sequential cast path.");
                }
            }
        }
    }
}
