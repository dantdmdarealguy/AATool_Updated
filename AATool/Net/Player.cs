using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AATool.Configuration;
using AATool.Net.Requests;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AATool.Net
{
    public struct CrackedPlayer
    {
        public Uuid Id;
        public string Name;
        public string SkinUrl;
    }

    public static class Player
    {
        public static readonly Dictionary<string, Uuid> IdCache = new ();
        public static readonly Dictionary<Uuid, string> NameCache = new ();

        public static readonly Dictionary<string, CrackedPlayer> CrackedIdCache = new(); // Name -> CrackedPlayer
        public static readonly Dictionary<Uuid, CrackedPlayer> CrackedNameCache = new(); // Uuid -> CrackedPlayer

        public static readonly Dictionary<Uuid, Color> IdColorCache = new ();
        public static readonly Dictionary<string, Color> NameColorCache = new ();

        public static readonly HashSet<string> NamesAlreadyRequested = new ();
        public static readonly HashSet<Uuid> IdentitiesAlreadyRequested = new ();

        private static bool IdentityCacheInvalidatedPrivate { get; set; }
        public static bool IdentityCacheInvalidated { get; set; }

        public static bool TryGetUuid(string name, out Uuid id)
        {
            if (Uuid.TryParse(name, out id)) return true;
            if (IdCache.TryGetValue(name ?? "", out id)) return true;
            if (CrackedIdCache.TryGetValue(name ?? "", out CrackedPlayer crackedPlayer))
            {
                id = crackedPlayer.Id;
                return true;
            }
            return false;
        }

        public static bool TryGetName(Uuid id, out string name)
        {
            if (NameCache.TryGetValue(id, out name)) return true;
            if (CrackedNameCache.TryGetValue(id, out CrackedPlayer crackedPlayer))
            {
                name = crackedPlayer.Name;
                return true;
            }
            return false;
        }

        public static bool TryGetColor(Uuid id, out Color color) => id != Uuid.Empty & IdColorCache.TryGetValue(id, out color);
        public static bool TryGetColor(string name, out Color color) => NameColorCache.TryGetValue(name ?? "", out color);

        public static bool TryGetSkinUrl(Uuid id, out string skinUrl)
        {
            skinUrl = null;
            if (CrackedNameCache.TryGetValue(id, out CrackedPlayer crackedPlayer) && !string.IsNullOrEmpty(crackedPlayer.SkinUrl))
            {
                skinUrl = crackedPlayer.SkinUrl;
                return true;
            }
            return false;
        }

        public static bool ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length is < 3 or > 16)
                return false;

            char character;
            for (int i = 0; i < name.Length; i++)
            {
                character = name[i];
                if (!char.IsLetter(character) && !char.IsNumber(character) && character is not '_')
                    return false;
            }
            return true;
        }

        public static async Task<Uuid> FetchUuidAsync(string name)
        {
            if (!ValidateName(name)) return Uuid.Empty;
            if (IdCache.TryGetValue(name, out Uuid id)) return id;
            if (CrackedIdCache.TryGetValue(name, out CrackedPlayer cachedCrackedPlayer)) return cachedCrackedPlayer.Id;

            // 1. Try Mojang API
            using (HttpClient client = new() { Timeout = TimeSpan.FromMilliseconds(Protocol.Requests.TimeoutNormalMs) })
            {
                try
                {
                    string response = await client.GetStringAsync(Paths.Web.GetUuidUrl(name));
                    if (!string.IsNullOrEmpty(response))
                    {
                        var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
                        if (Uuid.TryParse(values["id"], out id))
                        {
                            Cache(id, name); // Cache Mojang identity
                            new AvatarRequest(id).EnqueueOnce(); // Request Mojang avatar
                            return id;
                        }
                    }
                }
                catch { /* Mojang fetch failed, try Ely.by */ }
            }

            // 2. If Mojang failed, try Ely.by
            try
            {
                CrackedPlayer crackedPlayer = await FetchCrackedIdentityAsync(name);
                if (crackedPlayer.Id != Uuid.Empty)
                {
                    Cache(crackedPlayer); // Cache Ely.by identity
                    // AvatarRequest for Ely.by skin will be handled by ElyByAvatarRequest
                    return crackedPlayer.Id;
                }
            }
            catch { /* Ely.by fetch failed */ }

            return Uuid.Empty;
        }

        public static async Task<CrackedPlayer> FetchCrackedIdentityAsync(string name)
        {
            if (!ValidateName(name)) return new CrackedPlayer { Id = Uuid.Empty };
            if (CrackedIdCache.TryGetValue(name, out CrackedPlayer cachedPlayer)) return cachedPlayer;

            using (HttpClient client = new() { Timeout = TimeSpan.FromMilliseconds(Protocol.Requests.TimeoutNormalMs) })
            {
                try
                {
                    string response = await client.GetStringAsync(Paths.Web.GetElyByUserInfoUrl(name));
                    if (string.IsNullOrEmpty(response)) return new CrackedPlayer { Id = Uuid.Empty };

                    JObject json = JObject.Parse(response);
                    if (json["uuid"] is not null && json["username"] is not null)
                    {
                        if (Uuid.TryParse(json["uuid"].ToString(), out Uuid id))
                        {
                            string skinUrl = json["skin"]?["url"]?.ToString();
                            CrackedPlayer player = new CrackedPlayer
                            {
                                Id = id,
                                Name = json["username"].ToString(),
                                SkinUrl = skinUrl
                            };
                            Cache(player);
                            return player;
                        }
                    }
                }
                catch { }
            }
            return new CrackedPlayer { Id = Uuid.Empty };
        }

        public static void Cache(Uuid id, string name)
        {
            if (id == Uuid.Empty)
                return;

            if (!NameCache.ContainsKey(id) && !string.IsNullOrEmpty(name))
                NameCache[id] = name;
            if (name is not null && !IdCache.ContainsKey(name) && id != Uuid.Empty)
                IdCache[name] = id;

            if (name == Config.Tracking.SoloFilterName)
                Config.Tracking.SoloFilterName.InvokeChange();

            IdentityCacheInvalidatedPrivate = true;
        }

        public static void Cache(CrackedPlayer player)
        {
            if (player.Id == Uuid.Empty || string.IsNullOrEmpty(player.Name)) return;

            CrackedIdCache[player.Name] = player;
            CrackedNameCache[player.Id] = player;

            IdentityCacheInvalidatedPrivate = true;
        }

        public static void SetFlags()
        {
            IdentityCacheInvalidated = IdentityCacheInvalidatedPrivate;
        }

        public static void ClearFlags()
        {
            if (IdentityCacheInvalidated)
                IdentityCacheInvalidated = IdentityCacheInvalidatedPrivate = false;
        }

        public static void Cache(Uuid id, Color color)
        {
            IdColorCache[id] = color;
        }

        public static void Cache(string name, Color color)
        {
            NameColorCache[name] = color;
        }

        public static void FetchIdentityAsync(Uuid id)
        {
            if (IdentitiesAlreadyRequested.Contains(id)) return;
            if (NameCache.ContainsKey(id)) return;
            if (CrackedNameCache.ContainsKey(id)) return;

            IdentitiesAlreadyRequested.Add(id);
            new NameRequest(id).EnqueueOnce();
            new AvatarRequest(id).EnqueueOnce(); // This will now handle both premium and cracked skins
        }

        public static void FetchIdentityAsync(string name)
        {
            if (NamesAlreadyRequested.Contains(name)) return;
            if (IdCache.ContainsKey(name)) return;
            if (CrackedIdCache.ContainsKey(name)) return;

            NamesAlreadyRequested.Add(name);
            new UuidRequest(name, true).EnqueueOnce(); // This will use the updated FetchUuidAsync
        }
    }
}