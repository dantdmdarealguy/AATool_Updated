using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;
using AATool.Configuration;
using AATool.Net;
using AATool.Saves;
using AATool.Winforms.Forms;

namespace AATool.Winforms.Controls
{
    public partial class CTrackerSettings : UserControl
    {
        private bool loaded;

        private static Image SoloAvatar;
        private CancellationTokenSource cancelSource;
        private readonly ContextMenuStrip excludePlayersMenu = new();
        private readonly Button excludePlayersButton = new();
        private readonly Label excludePlayersLabel = new();
        private readonly Label playerListModeLabel = new();
        private readonly ComboBox playerListModeCombo = new();

        public CTrackerSettings()
        {
            this.InitializeComponent();
            this.components.Add(this.excludePlayersMenu);

            this.excludePlayersLabel.AutoSize = true;
            this.excludePlayersLabel.Location = new Point(4, 68);
            this.excludePlayersLabel.Margin = new Padding(3, 6, 3, 0);
            this.excludePlayersLabel.Name = "excludePlayersLabel";
            this.excludePlayersLabel.Text = "Exclude Players:";

            this.excludePlayersButton.Location = new Point(7, 84);
            this.excludePlayersButton.Name = "excludePlayersButton";
            this.excludePlayersButton.Size = new Size(98, 20);
            this.excludePlayersButton.Text = "Select...";
            this.excludePlayersButton.UseVisualStyleBackColor = true;
            this.excludePlayersButton.Click += this.OnClicked;
            this.excludePlayersMenu.Closing += this.OnExcludePlayersMenuClosing;

            this.playerListModeLabel.AutoSize = true;
            this.playerListModeLabel.Location = new Point(80, 19);
            this.playerListModeLabel.Margin = new Padding(3, 6, 3, 0);
            this.playerListModeLabel.Name = "playerListModeLabel";
            this.playerListModeLabel.Text = "Mode:";

            this.playerListModeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            this.playerListModeCombo.Items.AddRange(new object[] {
                "Exclude",
                "Include"
            });
            this.playerListModeCombo.Location = new Point(79, 35);
            this.playerListModeCombo.Name = "playerListModeCombo";
            this.playerListModeCombo.Size = new Size(91, 21);
            this.playerListModeCombo.SelectedIndexChanged += this.OnIndexChanged;

            this.groupBox5.Controls.Add(this.excludePlayersLabel);
            this.groupBox5.Controls.Add(this.excludePlayersButton);
            this.groupBox5.Controls.Add(this.playerListModeLabel);
            this.groupBox5.Controls.Add(this.playerListModeCombo);
            this.groupBox5.Width = 178;
        }

        public void LoadSettings()
        {
            this.loaded = false;
            this.autoVersion.Checked = Config.Tracking.AutoDetectVersion;
            this.UpdateCategoryControls();

            this.worldRemote.Checked = Config.Tracking.UseSftp;
            this.worldLocal.Checked = !Config.Tracking.UseSftp;
            this.manualChecklist.Checked = Config.Tracking.ManualChecklistMode;

            this.filterCombined.Checked = Config.Tracking.Filter == ProgressFilter.Combined;
            this.filterSolo.Checked = Config.Tracking.Filter == ProgressFilter.Solo;
            this.filterSoloName.Text = Config.Tracking.SoloFilterName;
            this.playerListModeCombo.SelectedIndex = Tracker.GetPlayerFilterMode() == PlayerListMode.Include
                ? 1
                : 0;

            this.trackActiveInstance.Checked = Config.Tracking.Source == TrackerSource.ActiveInstance;
            this.trackCustomSavesFolder.Checked = Config.Tracking.Source == TrackerSource.CustomSavesPath;
            this.TrackSpecificWorld.Checked = Config.Tracking.Source == TrackerSource.SpecificWorld;

            this.customSavesPath.Text = Config.Tracking.CustomSavesPath;
            this.customWorldPath.Text = Config.Tracking.CustomWorldPath;

            this.gameVersion.Text = Config.Tracking.GameVersion;

            this.enableOpenTracker.Checked = Config.Tracking.BroadcastProgress;

            this.sftpHost.Text = Config.Sftp.Host;
            this.sftpPort.Text = Config.Sftp.Port.Value.ToString();
            this.sftpUser.Text = Config.Sftp.Username;
            this.sftpPass.Text = Config.Sftp.Password;
            this.sftpRoot.Text = Config.Sftp.ServerRoot;
            this.sftpAutoSaveMinutes.Value = Config.Sftp.AutoSaveMinutes;
            this.sftpType.Text = Config.Sftp.Linux ? "Linux" : "Windows";

            this.UpdateSaveGroupPanel();
            this.UpdateFilterPanel();
            this.UpdateExcludedPlayersSummary();

            if (SoloAvatar is null)
            {
                this.cancelSource?.Cancel();
                this.cancelSource = new CancellationTokenSource();
                this.TryUpdateSoloFilterAsync(this.cancelSource.Token);
            }
            else
            {
                this.soloAvatar.Image = SoloAvatar;
            }
            this.loaded = true;
        }

        private void SaveSettings()
        {
            if (!this.loaded)
                return;

            if (this.trackActiveInstance.Checked)
                Config.Tracking.Source.Set(TrackerSource.ActiveInstance);
            else if (this.trackCustomSavesFolder.Checked)
                Config.Tracking.Source.Set(TrackerSource.CustomSavesPath);
            else if (this.TrackSpecificWorld.Checked)
                Config.Tracking.Source.Set(TrackerSource.SpecificWorld);

            Config.Tracking.CustomSavesPath.Set(this.customSavesPath.Text);
            Config.Tracking.CustomWorldPath.Set(this.customWorldPath.Text);

            if (this.filterCombined.Checked)
                Config.Tracking.Filter.Set(ProgressFilter.Combined);
            else if (this.filterSolo.Checked)
                Config.Tracking.Filter.Set(ProgressFilter.Solo);

            Config.Tracking.SoloFilterName.Set(this.filterSoloName.Text);
            Config.Tracking.PlayerFilterMode.Set(this.playerListModeCombo.SelectedIndex == 1
                ? PlayerListMode.Include
                : PlayerListMode.Exclude);

            TrackerSource source = this.trackActiveInstance.Checked
                ? TrackerSource.ActiveInstance
                : this.trackCustomSavesFolder.Checked
                    ? TrackerSource.CustomSavesPath
                    : TrackerSource.SpecificWorld;
            Config.Tracking.Source.Set(source);

            Config.Tracking.ManualChecklistMode.Set(this.manualChecklist.Checked);
            Config.Tracking.UseSftp.Set(this.worldRemote.Checked);
            Config.Tracking.AutoDetectVersion.Set(this.autoVersion.Checked);
            Config.Tracking.BroadcastProgress.Set(this.enableOpenTracker.Checked);
            Config.Tracking.TrySave();

            if (int.TryParse(this.sftpPort.Text, out int port))
                Config.Sftp.Port.Set(port);

            Config.Sftp.Host.Set(this.sftpHost.Text);
            Config.Sftp.Username.Set(this.sftpUser.Text);
            Config.Sftp.Password.Set(this.sftpPass.Text);
            Config.Sftp.ServerRoot.Set(this.sftpRoot.Text);
            Config.Sftp.AutoSaveMinutes.Set((int)Math.Max(1, this.sftpAutoSaveMinutes.Value));
            Config.Sftp.Linux.Set(this.sftpType.Text == "Linux");
            Config.Sftp.TrySave();
        }

        public void InvalidateSettings()
        {
            this.LoadSettings();
        }

        private void UpdateCategoryControls()
        {
            this.category.Text    = Tracker.Category.Name;
            this.category.Enabled = !Peer.IsClient;
            if (Config.Tracking.GameCategory.Changed || !this.loaded)
            {
                this.gameVersion.Items.Clear();
                foreach (string version in Tracker.Category.GetSupportedVersions().OrderBy(this.SortVersion))
                    this.gameVersion.Items.Add(version);
            }
            if (Config.Tracking.GameVersion.Changed || !this.loaded)
            {
                this.gameVersion.Text = Tracker.Category.CurrentVersion;
            }
            this.gameVersion.Enabled = !this.autoVersion.Checked && !Peer.IsClient && this.gameVersion.Items.Count > 1;
        }


        private void UpdateSaveGroupPanel()
        {
            if (this.worldLocal.Checked)
            {
                this.localGroup.Enabled = true;
                this.localGroup.Visible = true;
                this.remoteGroup.Visible = false;
            }
            else if (this.worldRemote.Checked)
            {
                this.remoteGroup.Enabled = true;
                this.localGroup.Visible = false;
                this.remoteGroup.Visible = true;
            }
            else if (this.manualChecklist.Checked)
            {
                this.localGroup.Enabled = false;
                this.remoteGroup.Enabled = false;
            }
        }

        private void UpdateFilterPanel()
        {
            this.filterSoloName.Enabled = this.filterSolo.Checked;
            this.label10.Visible = this.filterSolo.Checked;
            this.filterSoloName.Visible = this.filterSolo.Checked;
            this.soloAvatar.Visible = this.filterSolo.Checked;

            this.playerListModeLabel.Visible = this.filterCombined.Checked;
            this.playerListModeCombo.Visible = this.filterCombined.Checked;
            this.playerListModeCombo.Enabled = this.filterCombined.Checked && !Peer.IsClient;
            this.excludePlayersLabel.Visible = this.filterCombined.Checked;
            this.excludePlayersButton.Visible = this.filterCombined.Checked;
            this.excludePlayersLabel.Text = this.playerListModeCombo.SelectedIndex == 1
                ? "Include Players:"
                : "Exclude Players:";
            this.UpdateExcludedPlayersSummary();
        }

        private Version SortVersion(string version)
        {
            if (Version.TryParse(version, out Version parsed))
                return parsed;

            string numeric = new string(version.TakeWhile(ch => char.IsDigit(ch) || ch == '.').ToArray()).Trim('.');
            return Version.TryParse(numeric, out parsed)
                ? parsed
                : new Version(0, 0);
        }

        private string GetPlayerLabel(Uuid playerId)
        {
            if (Peer.TryGetLobby(out Lobby lobby) && lobby.TryGetUser(playerId, out User user) && !string.IsNullOrWhiteSpace(user.Name))
                return user.Name;

            return Player.TryGetName(playerId, out string name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : playerId.String;
        }

        private void PopulateExcludedPlayersMenu()
        {
            this.excludePlayersMenu.Items.Clear();
            HashSet<Uuid> selectedPlayers = Tracker.GetSelectedPlayers();
            HashSet<Uuid> hostSelectedPlayers = Tracker.GetPlayerFilterMode() == PlayerListMode.Include
                ? Tracker.GetHostIncludedPlayers()
                : Tracker.GetHostExcludedPlayers();

            foreach (Uuid playerId in Tracker.GetTrackedPlayers().OrderBy(this.GetPlayerLabel))
            {
                if (playerId == Uuid.Empty)
                    continue;

                var item = new ToolStripMenuItem(this.GetPlayerLabel(playerId)) {
                    Tag = playerId,
                    Checked = selectedPlayers.Contains(playerId),
                    CheckOnClick = true
                };
                if (Peer.IsClient && hostSelectedPlayers.Contains(playerId))
                {
                    item.Enabled = false;
                    item.ToolTipText = "Locked by the host";
                }
                item.CheckedChanged += this.OnExcludedPlayerCheckedChanged;
                this.excludePlayersMenu.Items.Add(item);
            }

            if (this.excludePlayersMenu.Items.Count is 0)
            {
                this.excludePlayersMenu.Items.Add(new ToolStripMenuItem("No tracked players") {
                    Enabled = false
                });
            }
        }

        private void UpdateExcludedPlayersSummary()
        {
            int count = Tracker.GetSelectedPlayers().Count;
            string noun = Tracker.GetPlayerFilterMode() == PlayerListMode.Include
                ? "included"
                : "excluded";
            this.excludePlayersButton.Text = count switch {
                0 => "Select...",
                1 => $"1 {noun}",
                _ => $"{count} {noun}"
            };
        }

        private void SaveExcludedPlayers()
        {
            if (!this.loaded)
                return;

            HashSet<Uuid> localSelectedPlayers = this.playerListModeCombo.SelectedIndex == 1
                ? Tracker.GetLocalIncludedPlayers()
                : Tracker.GetLocalExcludedPlayers();
            IEnumerable<string> selected = this.excludePlayersMenu.Items
                .OfType<ToolStripMenuItem>()
                .Where(x => x.Tag is Uuid id && x.Checked && (x.Enabled || localSelectedPlayers.Contains(id)))
                .Select(x => ((Uuid)x.Tag).String);

            if (this.playerListModeCombo.SelectedIndex == 1)
                Config.Tracking.IncludedPlayers.Set(string.Join(",", selected));
            else
                Config.Tracking.ExcludedPlayers.Set(string.Join(",", selected));

            Config.Tracking.TrySave();
            this.UpdateExcludedPlayersSummary();
        }

        private void TogglePassword()
        {
            bool hide = true;
            if (this.sftpPass.UseSystemPasswordChar)
            {
                //show confirmation dialog
                string message = "Be careful about showing SFTP login credentials on stream! ♥\nAre you sure you want to unmask the username and password fields?";
                string title   = "SFTP Credentials Reveal Confirmation";
                DialogResult result = MessageBox.Show(this, message, title,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                hide = result is not DialogResult.Yes;
            }
            this.sftpPass.UseSystemPasswordChar = hide;
            this.sftpUser.UseSystemPasswordChar = hide;
            this.toggleCredentials.Text = hide
                ? "Show Login"
                : "Hide Login";
        }

        private void OnClicked(object sender, EventArgs e)
        {
            if (sender == this.browseSaves)
            {
                using var dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() == DialogResult.OK)
                    this.customSavesPath.Text = dialog.SelectedPath;
            }
            else if (sender == this.browseWorld)
            {
                using var dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() == DialogResult.OK)
                    this.customWorldPath.Text = dialog.SelectedPath;
            }
            else if (sender == this.sftpValidate)
            {
                MinecraftServer.Sync();
            }
            else if (sender == this.toggleCredentials)
            {
                this.TogglePassword();
            }
            else if (sender == this.excludePlayersButton)
            {
                this.PopulateExcludedPlayersMenu();
                this.excludePlayersMenu.Show(this.excludePlayersButton, new Point(0, this.excludePlayersButton.Height));
                return;
            }
            else if (sender == this.configureOpenTracker)
            {
                using (var dialog = new FOpenTrackerSetup())
                    dialog.ShowDialog();
            }
            this.SaveSettings();
        }

        private void OnCheckChanged(object sender, EventArgs e)
        {
            if (!this.loaded)
                return;

            if (sender == this.worldLocal || sender == this.worldRemote)
            {
                this.UpdateSaveGroupPanel();
            }
            else if (sender == this.filterCombined || sender == this.filterSolo)
            {
                this.UpdateFilterPanel();
            }
            else if (sender == this.autoVersion)
            {
                this.UpdateCategoryControls();
            }
            this.SaveSettings();
        }

        private void OnIndexChanged(object sender, EventArgs e)
        {
            if (!this.loaded)
                return;

            if (sender == this.category)
            {
                Tracker.TrySetCategory(this.category.Text);
            }
            else if (sender == this.gameVersion)
            {
                Tracker.TrySetVersion(this.gameVersion.Text);
            }
            else if (sender == this.playerListModeCombo)
            {
                this.SaveSettings();
                this.UpdateFilterPanel();
                return;
            }
            this.SaveSettings();
        }

        private void OnTextChanged(object sender, EventArgs e) 
        {
            if (sender == this.filterSoloName)
            {
                //cancel old requests and start a new one
                this.cancelSource?.Cancel();
                this.cancelSource = new CancellationTokenSource();

                //cooldown timer to prevent spamming requests for every letter of someone's name
                this.keyboardTimer.Start();
            }
            this.SaveSettings();
        }

        private void OnLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (sender == this.sftpCompatibility)
            {
                string title = "SFTP Compatibility Information";
                string body = "Remote tracking over SFTP has only been officially tested on DedicatedMC, " +
                    "although other hosts should work as well.";
                MessageBox.Show(this, body, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            //enough time has passed since last character was typed
            this.keyboardTimer.Stop();
            this.TryUpdateSoloFilterAsync(this.cancelSource.Token);
        }

        private async void TryUpdateSoloFilterAsync(CancellationToken? cancelToken = null)
        {
            if (!Uuid.TryParse(this.filterSoloName.Text, out Uuid id))
            {
                if (Player.ValidateName(this.filterSoloName.Text))
                    id = await Player.FetchUuidAsync(this.filterSoloName.Text);
            }

            if (id == Uuid.Empty)
            {
                this.soloAvatar.Image = null;
                return;
            }

            if (this.soloAvatar.Image is null || (this.soloAvatar.Image.Tag is Uuid current && id != current))
            {
                try
                {
                    //asynchronously pull player avatar from the internet
                    string url = Paths.Web.GetAvatarUrl(id.String, 16);
                    using HttpClient http = new ();
                    using HttpResponseMessage responce = await http.GetAsync(new Uri(url), cancelToken ?? CancellationToken.None);
                    using Stream stream = await responce.Content.ReadAsStreamAsync();
                    this.soloAvatar.Image = new Bitmap(stream) {
                        Tag = id
                    };
                    SoloAvatar = this.soloAvatar.Image;
                }
                catch { }
            }
        }

        private void OnValueChanged(object sender, EventArgs e)
        {
            this.SaveSettings();
        }

        private void OnExcludedPlayerCheckedChanged(object sender, EventArgs e)
        {
            this.SaveExcludedPlayers();
        }

        private void OnExcludePlayersMenuClosing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                e.Cancel = true;
        }
    }
}
