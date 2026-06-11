using AATool.Data;
using AATool.Net;
using AATool.UI.Screens;
using System.Collections.Generic;
using System.Linq;

namespace AATool.UI.Controls
{
    public class UILobby : UIPanel
    {
        private readonly Dictionary<Uuid, UIPlayer> players;

        private UIFlowPanel flowPlayers;

        public UILobby()
        {
            this.players = new ();
        }

        public override void InitializeRecursive(UIScreen screen)
        {
            this.BuildFromTemplate();
            this.flowPlayers = this.First<UIFlowPanel>("player_list");
            base.InitializeRecursive(screen);
        }

        public void Clear()
        {
            this.flowPlayers.Children.Clear();
            this.players.Clear();
        }

        protected override void UpdateThis(Time time)
        {
            bool changed = false;
            if (!Peer.IsRunning || !Peer.TryGetLobby(out Lobby lobby))
                return;

            HashSet<Uuid> visiblePlayers = Tracker.GetAllPlayers();

            foreach (Uuid key in visiblePlayers)
            {
                if (Tracker.IsPlayerExcluded(key))
                {
                    if (this.players.ContainsKey(key))
                    {
                        this.flowPlayers.RemoveControl(this.players[key]);
                        this.players.Remove(key);
                        changed = true;
                    }
                    continue;
                }

                if (!this.players.ContainsKey(key))
                {
                    if (!lobby.TryGetUser(key, out User user))
                        user = new User(key, string.Empty, Player.TryGetName(key, out string name) ? name : key.String);

                    var control = new UIPlayer(user);
                    control.InitializeRecursive(this.Root());
                    this.players[key] = control;
                    this.flowPlayers.AddControl(control);
                    changed = true;
                }
            }

            foreach (Uuid id in this.players.Keys.ToArray())
            {
                if (!visiblePlayers.Contains(id))
                {
                    this.flowPlayers.RemoveControl(this.players[id]);
                    this.players.Remove(id);
                    changed = true;
                }
            }

            if (changed)
                this.flowPlayers.ResizeRecursive(this.flowPlayers.Bounds);
        }
    }
}
