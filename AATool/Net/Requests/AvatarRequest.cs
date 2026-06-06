using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using AATool.Data.Speedrunning;
using AATool.Graphics;
using AATool.Utilities;
using Microsoft.Xna.Framework.Graphics;

namespace AATool.Net.Requests
{
    public sealed class AvatarRequest : NetRequest
    {
        public static int Downloads { get; private set; }

        private readonly Uuid id;
        private readonly string name;
        private readonly bool isFallback;

        public AvatarRequest(Uuid player, bool isFallback = false) : 
            base (isFallback ? Paths.Web.GetAvatarUrlFallback(player, 8) : Paths.Web.GetAvatarUrl(player, 8))
        {
            this.id = player;
            this.isFallback = isFallback;
            Player.TryGetName(this.id, out this.name);
            this.name = this.name?.ToLower();
        }

        public AvatarRequest(string name) : base(Paths.Web.GetAvatarUrl(Leaderboard.GetRealName(name).ToLower(), 8))
        {
            this.name = Leaderboard.GetRealName(name).ToLower();
            Player.TryGetUuid(this.name, out this.id);
        }

        public override async Task<bool> DownloadAsync()
        {
            string requestUrl = this.Url; // Default to Mojang/Minotar URL

            // Check if this is a cracked player with a known skin URL from Ely.by
            if (Player.TryGetSkinUrl(this.id, out string elyBySkinUrl))
            {
                requestUrl = elyBySkinUrl;
                Debug.Log(Debug.RequestSection, $"{Outgoing} Requesting Ely.by skin for cracked player {this.id.ShortString ?? this.name} from {requestUrl}");
            }
            else
            {
                // Logging for Mojang/Minotar request
                if (Player.TryGetName(this.id, out string name))
                    Debug.Log(Debug.RequestSection, $"{Outgoing} Requested avatar for \"{name}\" from {requestUrl}");
                else
                    Debug.Log(Debug.RequestSection, $"{Outgoing} Requested avatar for {this.id.ShortString ?? this.name} from {requestUrl}");
            }
            
            Downloads++;
            this.BeginTiming();

            try
            {
                // Download texture and add to atlas
                using (Stream response = await Client.GetStreamAsync(requestUrl))
                {
                    this.EndTiming();
                    return this.HandleResponse(response);
                }
            }
            catch (OperationCanceledException)
            {
                // Request canceled, nothing left to do here
                Debug.Log(Debug.RequestSection, $"-- Avatar request cancelled for {this.id.ShortString ?? this.name}");
            }
            catch (HttpRequestException e)
            {
                // Error getting response, try other url (only for Mojang/Minotar fallbacks)
                Debug.Log(Debug.RequestSection, $"-- Avatar request failed for {this.id.ShortString ?? this.name}: {e.Message}");

                // If it was a Mojang/Minotar request and not a fallback already, try the fallback URL
                if (!this.isFallback && this.id != Uuid.Empty && !Player.CrackedNameCache.ContainsKey(this.id))
                    new AvatarRequest(this.id, true).EnqueueOnce();
            }
            catch (Exception e)
            {
                Debug.Log(Debug.RequestSection, $"-- Unexpected error during avatar request for {this.id.ShortString ?? this.name}: {e.Message}");
            }
            this.EndTiming();
            return false;
        }

        private bool HandleResponse(Stream avatarStream)
        {
            Texture2D texture = null;
            try
            {
                texture = Texture2D.FromStream(Main.GraphicsManager.GraphicsDevice, avatarStream);

                // Cache by UUID string
                if (this.id != Uuid.Empty)
                {
                    string uuidSprite = $"avatar-{this.id}";
                    texture.Tag = uuidSprite;
                    SpriteSheet.Pack(texture);
                    Debug.Log(Debug.RequestSection, $"{Incoming} Received avatar for {this.id.ShortString} in {this.ResponseTime}");
                }
                // Cache by name if no UUID available
                else if (!string.IsNullOrEmpty(this.name))
                {
                    string nameSprite = $"avatar-{this.name}";
                    texture.Tag = nameSprite;
                    SpriteSheet.Pack(texture);
                    Debug.Log(Debug.RequestSection, $"{Incoming} Received avatar for \"{this.name}\" in {this.ResponseTime}");
                }
                
                return true;
            }
            catch (ArgumentException)
            {
                // Safely ignore malformed stream and move on
                Debug.Log(Debug.RequestSection, $"{Incoming} Received invalid avatar for {this.id.ShortString ?? this.name} in {this.ResponseTime}");
                return false;
            }
            finally
            {
                // Compute average color for player-specific glow colors
                if (this.id != Uuid.Empty && texture != null)
                {
                    Player.Cache(this.id, ColorHelper.GetAccent(texture));
                }
                texture?.Dispose();
            }
        }
    }
}