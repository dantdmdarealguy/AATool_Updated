using System.Collections.Generic;
using Newtonsoft.Json;

namespace AATool.Net
{
    [JsonObject]
    public sealed class Lobby
    {
        [JsonProperty] public readonly Dictionary<Uuid, User> Users;
        [JsonProperty] public readonly Dictionary<string, Uuid> Designations;
        [JsonProperty] public PlayerListMode PlayerFilterMode { get; set; }
        [JsonProperty] public HashSet<Uuid> ExcludedPlayers { get; set; }
        [JsonProperty] public HashSet<Uuid> IncludedPlayers { get; set; }
        [JsonProperty] private Uuid hostId;

        public int UserCount => this.Users.Count;

        public Lobby()
        {
            this.Users        = new ();
            this.Designations = new ();
            this.PlayerFilterMode = PlayerListMode.Exclude;
            this.ExcludedPlayers = new ();
            this.IncludedPlayers = new ();
        }

        public bool TryGetHost(out User host) => this.Users.TryGetValue(this.hostId, out host);

        public void SetHost(User user)
        {
            this.Users[user.Id] = user;
            this.hostId = user.Id;
        }

        public static Lobby FromJsonString(string jsonString)
        {
            Lobby lobby = JsonConvert.DeserializeObject<Lobby>(jsonString);
            if (lobby is null)
                return new Lobby();
            lobby.ExcludedPlayers ??= new HashSet<Uuid>();
            lobby.IncludedPlayers ??= new HashSet<Uuid>();

            //attempt to load player identities
            foreach (Uuid id in lobby.Users.Keys)
                Player.FetchIdentityAsync(id);
            foreach (Uuid id in lobby.ExcludedPlayers)
                Player.FetchIdentityAsync(id);
            foreach (Uuid id in lobby.IncludedPlayers)
                Player.FetchIdentityAsync(id);
            return lobby;
        }


        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public bool TryGetUser(Uuid id, out User user) => this.Users.TryGetValue(id, out user);

        public void Add(User user)    => this.Users[user.Id] = user;

        public void Remove(User user) => this.Users.Remove(user.Id);
    }
}
