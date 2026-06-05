using System.Collections.Generic;
using System.Linq;
using AATool.Data.Objectives;
using AATool.Net;
using AATool.Utilities;

namespace AATool.Data.Progress
{
    public class WorldState : ProgressState
    {
        public static readonly WorldState Empty = new ();

        public Dictionary<Uuid, Contribution> Players { get; set; }

        public WorldState() : base()
        {
            this.Players = new ();
        }

        public WorldState(NetworkState state) : this()
        {
            //copy co-op state
            foreach (NetworkContribution player in state.Players)
            {
                //individual progress
                var contribution = new Contribution(player);
                this.Players[contribution.Player] = contribution;

                //combined advancements
                foreach (KeyValuePair<string, Completion> advancement in contribution.Advancements)
                {
                    if (!this.Advancements.TryGetValue(advancement.Key, out Completion first) || advancement.Value.Before(first.Timestamp))
                        this.Advancements[advancement.Key] = advancement.Value;
                }

                //combined criteria
                foreach (KeyValuePair<string, Completion> criterion in contribution.Criteria)
                {
                    if (!this.Criteria.TryGetValue(criterion.Key, out Completion first) || criterion.Value.Before(first.Timestamp))
                        this.Criteria[criterion.Key] = criterion.Value;
                }

                //combined stats
                this.CopyStats(player.Pickup, this.PickupCounts);
                this.CopyStats(player.Drop, this.DropCounts);
                this.CopyStats(player.Mine, this.MineCounts);
                this.CopyStats(player.Craft, this.CraftCounts);
                this.CopyStats(player.Use, this.UseCounts);
                this.CopyStats(player.Kill, this.KillCounts);

                //enchanted golden apple
                this.ObtainedGodApple |= contribution.ObtainedGodApple;
                //lapis
                this.ObtainedLapis |= contribution.ObtainedLapis;
            }

            this.InGameTime = state.InGameTime;
            this.KilometersFlown = state.KilometersFlown;
            this.ItemsEnchanted = state.ItemsEnchanted;
        }

        public WorldState FilterPlayers(HashSet<Uuid> excludedPlayers)
        {
            if (excludedPlayers is null || excludedPlayers.Count is 0)
                return this;

            var filtered = new WorldState();
            foreach (Contribution contribution in this.Players.Values.Where(x => !excludedPlayers.Contains(x.Player)))
            {
                filtered.Players[contribution.Player] = contribution;

                foreach (KeyValuePair<string, Completion> advancement in contribution.Advancements)
                {
                    if (!filtered.Advancements.TryGetValue(advancement.Key, out Completion first) || advancement.Value.Before(first.Timestamp))
                        filtered.Advancements[advancement.Key] = advancement.Value;
                }

                foreach (KeyValuePair<string, Completion> criterion in contribution.Criteria)
                {
                    if (!filtered.Criteria.TryGetValue(criterion.Key, out Completion first) || criterion.Value.Before(first.Timestamp))
                        filtered.Criteria[criterion.Key] = criterion.Value;
                }

                this.CopyStats(contribution.PickupCounts, filtered.PickupCounts);
                this.CopyStats(contribution.DropCounts, filtered.DropCounts);
                this.CopyStats(contribution.MineCounts, filtered.MineCounts);
                this.CopyStats(contribution.CraftCounts, filtered.CraftCounts);
                this.CopyStats(contribution.UseCounts, filtered.UseCounts);
                this.CopyStats(contribution.KillCounts, filtered.KillCounts);

                if (contribution.InGameTime > filtered.InGameTime)
                    filtered.InGameTime = contribution.InGameTime;

                filtered.KilometersFlown += contribution.KilometersFlown;
                filtered.ItemsEnchanted += contribution.ItemsEnchanted;
                filtered.SaveAndQuits += contribution.SaveAndQuits;
                filtered.DamageDealt += contribution.DamageDealt;
                filtered.DamageTaken += contribution.DamageTaken;
                filtered.Sleeps += contribution.Sleeps;
                filtered.Deaths += contribution.Deaths;
                filtered.Jumps += contribution.Jumps;
                filtered.ObtainedGodApple |= contribution.ObtainedGodApple;
                filtered.ObtainedLapis |= contribution.ObtainedLapis;
            }

            // Only copy death messages if the main player is not excluded
            if (!excludedPlayers.Contains(Tracker.GetMainPlayer()))
            {
                foreach (string death in this.DeathMessages)
                    filtered.DeathMessages.Add(death);
            }

            return filtered;
        }

        private void CopyStats(Dictionary<string, int> source, Dictionary<string, int> destination)
        {
            foreach (KeyValuePair<string, int> statistic in source)
            {
                _= destination.TryGetValue(statistic.Key, out int existing);
                destination[statistic.Key] = existing + statistic.Value;
            }
        }

        public override HashSet<Completion> CompletionsOf(IObjective objective)
        {
            //compile a list of all players who have completed this objective
            var completionists = new HashSet<Completion>();
            if (objective is Advancement advancement)
            {
                foreach (KeyValuePair<Uuid, Contribution> player in this.Players)
                {
                    if (player.Value.Advancements.TryGetValue(advancement.Id, out Completion completion))
                        completionists.Add(completion);
                }
            }
            else if (objective is Criterion criterion)
            {
                foreach (KeyValuePair<Uuid, Contribution> player in this.Players)
                {
                    if (player.Value.Criteria.TryGetValue(Criterion.Key(criterion.Owner.Id, criterion.Id), out Completion completion))
                        completionists.Add(completion);
                }
            }
            else if (objective is Block block)
            {
                foreach (KeyValuePair<Uuid, Contribution> player in this.Players)
                {
                    if (player.Value.UseCounts.ContainsKey(block.Id) is true)
                        completionists.Add(new Completion(player.Key, default));

                    if (block.HasAlternateIds)
                    {
                        foreach (string id in block.AlternateIds)
                        {
                            if (player.Value.UseCounts.ContainsKey(id) is true)
                                completionists.Add(new Completion(player.Key, default));
                        }
                    }
                }
            }
            else if (objective is Death death)
            {
                // Only add completion if the main player is not excluded
                if (this.DeathMessages.Contains(death.Id) && !Tracker.IsPlayerExcluded(Tracker.GetMainPlayer()))
                    completionists.Add(new Completion(Tracker.GetMainPlayer(), default));
            }
            return completionists;
        }

        public void SyncDeathMessages()
        {
            if (!ActiveInstance.TryGetLog(out string log) || !Player.TryGetName(Tracker.GetMainPlayer(), out string name))
                return;

            log = log.ToLower();
            foreach (Death death in Tracker.Deaths.All.Values)
            {
                foreach (string message in death.Messages)
                {
                    if (log.Contains($"[server thread/info]: {name.ToLower()} {message}"))
                    {
                        this.DeathMessages.Add(death.Id);
                        break;
                    }
                }
            }
        }
    }
}