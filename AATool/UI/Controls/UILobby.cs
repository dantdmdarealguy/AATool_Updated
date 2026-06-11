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

            HashSet<Uuid> lobbyPlayers = new(lobby.Users.Keys);

            foreach (Uuid key in lobbyPlayers)
            {
                if (!this.players.ContainsKey(key))
                {
                    User user = lobby.Users[key];
                    var control = new UIPlayer(user);
                    control.InitializeRecursive(this.Root());
                    this.players[key] = control;
                    this.flowPlayers.AddControl(control);
                    changed = true;
                }
            }

            foreach (Uuid id in this.players.Keys.ToArray())
            {
                if (!lobbyPlayers.Contains(id))
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
