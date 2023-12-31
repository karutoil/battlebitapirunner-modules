using System;
using System.Text;
using System.Text.Json;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;

using BBRAPIModules;
using BattleBitAPI.Common;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Bluscream {
    #region Requires
    [RequireModule(typeof(Bluscream.GeoApi))]
    [RequireModule(typeof(Bluscream.SteamApi))]
    [RequireModule(typeof(Bluscream.BluscreamLib))]
    [RequireModule(typeof(Permissions.PlayerPermissions))]
    [RequireModule(typeof(Commands.CommandHandler))]
    #endregion
    [Module("Logger", "2.0.1")]
    public class Logger : BattleBitModule {
        public static ModuleInfo ModuleInfo = new() {
            Name = "Logger",
            Description = "Extensive customizable logging for the BattleBit Modular API",
            Version = new Version(2,0,1),
            Author = "Bluscream",
            WebsiteUrl = new Uri("https://github.com/Bluscream/battlebitapirunner-modules/"),
            UpdateUrl = new Uri("https://github.com/Bluscream/battlebitapirunner-modules/raw/master/modules/Logger.cs"),
            SupportUrl = new Uri("https://github.com/Bluscream/battlebitapirunner-modules/issues/new?title=Logger")
        };

        #region References
        [ModuleReference]
        public Commands.CommandHandler CommandHandler { get; set; }

        [ModuleReference]
        public GeoApi GeoApi { get; set; } = null!;

        [ModuleReference]
        public SteamApi SteamApi { get; set; } = null!;

        [ModuleReference]
#if DEBUG
        public Permissions.PlayerPermissions? PlayerPermissions { get; set; }
#else
        public dynamic? PlayerPermissions { get; set; }
#endif
        #endregion

        #region Fields
        internal HttpClient httpClient = new HttpClient();
        internal Random random = Random.Shared;
        #endregion

        #region Methods
        internal void LogToConsole(string msg) {
            if (string.IsNullOrWhiteSpace(msg)) return;
            Console.WriteLine(msg);
        }
        private async Task SendToWebhook(string webhookUrl, string msg) {
            if (string.IsNullOrWhiteSpace(msg)) return;
            bool success = false;
            while (!success) {
                var payload = new {
                    content = msg.Replace("@", "\\@")
                };
                var payloadJson = JsonSerializer.Serialize(payload);
                var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                HttpResponseMessage response;
                try { response = await this.httpClient.PostAsync(webhookUrl, content); } catch (Exception ex) {
                    // BluscreamLib.Log($"Failed to POST webhook: {ex.Message}");
                    return;
                }
                if (!response.IsSuccessStatusCode) {
                    Console.WriteLine($"Error sending webhook message. Status Code: {response.StatusCode}");
                    double waitSeconds = 1;
                    if (response.Headers.TryGetValues("X-RateLimit-Reset-After", out var values)) {
                        waitSeconds = double.Parse(values.First());
                    }
                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
                }
                success = response.IsSuccessStatusCode;
            }
        }
        internal void SayToAll(string msg) {
            if (this.Server is null || string.IsNullOrWhiteSpace(msg)) return;
            if (string.IsNullOrWhiteSpace(msg)) return;
            this.Server.SayToAllChat(msg);
        }
        internal void SayToPlayer(string msg, RunnerPlayer player) {
            if (this.Server is null || string.IsNullOrWhiteSpace(msg)) return;
            if (string.IsNullOrWhiteSpace(msg)) return;
            Server.SayToChat(msg, player);
        }
        internal void ModalMessage(string msg, RunnerPlayer player) {
            if (this.Server is null || string.IsNullOrWhiteSpace(msg)) return;
            if (string.IsNullOrWhiteSpace(msg)) return;
            player.Message(msg);
        }
        internal void UILogOnServer(string msg, Duration duration) {
            if (this.Server is null || string.IsNullOrWhiteSpace(msg)) return;
            var durationS = 1;
            switch (duration) {
                case Duration.Short:
                    durationS = 3; break;
                case Duration.Long:
                    durationS = 10; break;
            }
            this.Server.UILogOnServer(msg, durationS);
        }
        internal void Announce(string msg, Duration duration) {
            if (this.Server is null || !this.Server.IsConnected || string.IsNullOrWhiteSpace(msg)) return;
            try {
                switch (duration) {
                    case Duration.Short:
                        this.Server.AnnounceShort(msg); return;
                    case Duration.Long:
                        this.Server.AnnounceLong(msg); return;
                }
            } catch (Exception ex) {
                Console.WriteLine($"Got exception {ex.Message} while trying to announce to players");
            }
        }

        internal string FormatString(string input, RunnerServer? server = null, RunnerPlayer? player = null, RunnerPlayer? target = null, IpApi.Response? geoData = null, SteamWebApi.Response? steamData = null, ReportReason? reportReason = null, ChatChannel? chatChannel = null, string msg = null) {
            var now = string.IsNullOrWhiteSpace(Config.TimeStampFormat) ? "" : new DateTimeWithZone(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById(Config.TimeZone)).LocalTime.ToString(Config.TimeStampFormat);
            input = input.Replace("{now}", now);
            if (player is not null) {
                input = input.Replace("{player.Name}", player?.Name);
                input = input.Replace("{player.SteamID}", player?.SteamID.ToString());
                input = input.Replace("{player.str()}", player?.str());
                input = input.Replace("{player.fullstr()}", player?.fullstr());
                input = input.Replace("{player.IP}", player?.IP.ToString());
            }
            if (player is not null) {
                input = input.Replace("{target.Name}", target?.Name);
                input = input.Replace("{target.SteamID}", target?.SteamID.ToString());
                input = input.Replace("{target.str()}", target?.str());
                input = input.Replace("{target.fullstr()}", target?.fullstr());
                input = input.Replace("{target.IP}", target?.IP.ToString());
            }
            if (server is not null) {
                try { input = input.Replace("{server.Name}", server?.ServerName); } catch { }
                try { input = input.Replace("{server.AllPlayers.Count()}", server?.AllPlayers.Count().ToString()); } catch { }
                try { input = input.Replace("{server.str()}", server?.str()); } catch { }
            }
            if (geoData is not null) {
                input = input.Replace("{geoData.City}", geoData.City);
                input = input.Replace("{geoData.RegionName}", geoData.RegionName);
                input = input.Replace("{geoData.Country}", geoData.Country);
                input = input.Replace("{geoData.CountryCode}", geoData.CountryCode?.ToLowerInvariant());
                input = input.Replace("{geoData.Continent}", geoData.Continent);
                input = input.Replace("{geoData.Isp}", geoData.Isp);
                input = input.Replace("{geoData.Timezone}", geoData.Timezone);
                input = input.Replace("{geoData.Reverse}", geoData.Reverse);
                input = input.Replace("{geoData.ToJson()}", geoData.ToJson());
            }
            input = input.Replace("{reason}", reportReason?.ToString());
            input = input.Replace("{msg}", msg);
            input = input.Replace("{chatChannel}", chatChannel?.ToString());
            foreach (var replacement in Config.randomReplacements) {
                input = input.Replace($"{{random.{replacement.Key}}}", replacement.Value[random.Next(replacement.Value.Length)]);
            }
            return input; // Smart.Format(input, now=now, parms);
        }

        internal async void HandleEvent(LogConfigurationEntry config, RunnerPlayer? player = null, RunnerPlayer? target = null, IpApi.Response? geoData = null, SteamWebApi.Response? steamData = null, ReportReason? reportReason = null, ChatChannel? chatChannel = null, string _msg = null) {
            if (config.Console is not null && config.Console.Enabled && !string.IsNullOrWhiteSpace(config.Console.Message)) {
                LogToConsole(FormatString(config.Console.Message, server: this.Server, player: player, target: target, geoData: geoData, steamData: steamData, reportReason: reportReason, chatChannel: chatChannel, msg: _msg));
            }
            if (config.Discord is not null && config.Discord.Enabled && !string.IsNullOrWhiteSpace(config.Discord.WebhookUrl) && !string.IsNullOrWhiteSpace(config.Discord.Message)) {
                var msg = FormatString(config.Discord.Message, server: this.Server, player: player, target: target, geoData: geoData, steamData: steamData, reportReason: reportReason, chatChannel: chatChannel, msg: _msg);
                await SendToWebhook(config.Discord.WebhookUrl, msg);
            }
            try { var a = this.Server.IsConnected; } catch { return; }
            if (this.Server is null || !this.Server.IsConnected) return;
            if (config.Chat is not null && config.Chat.Enabled && !string.IsNullOrWhiteSpace(config.Chat.Message)) {
                var msg = FormatString(config.Chat.Message, server: this.Server, player: player, target: target, geoData: geoData, steamData: steamData, reportReason: reportReason, chatChannel: chatChannel, msg: _msg);
                if (this.PlayerPermissions is not null && config.Chat.Roles != Roles.None) {
                    try {
                        foreach (var _player in this.Server.AllPlayers) {
                            var playerRoles = this.PlayerPermissions.GetPlayerRoles(_player.SteamID);
                            if ((playerRoles & config.Chat.Roles) == 0) continue;
                            SayToPlayer(msg, player: _player);
                        }
                    } catch (Exception ex) {
                        Console.WriteLine($"Got exception {ex.Message} while trying to send message to players");
                    }
                } else SayToAll(msg);
            }
            if (config.Modal is not null && config.Modal.Enabled && !string.IsNullOrWhiteSpace(config.Modal.Message)) {
                var msg = FormatString(config.Modal.Message, server: this.Server, player: player, target: target, geoData: geoData, steamData: steamData, reportReason: reportReason, chatChannel: chatChannel, msg: _msg);
                foreach (var _player in this.Server.AllPlayers) {
                    if (this.PlayerPermissions is not null) {
                        var playerRoles = this.PlayerPermissions.GetPlayerRoles(_player.SteamID);
                        if ((playerRoles & config.Modal.Roles) == 0) continue;
                    }
                    ModalMessage(msg, player: _player);
                }
            }
            if (config.UILog is not null && config.UILog.Enabled && !string.IsNullOrWhiteSpace(config.UILog.Message)) {
                var msg = FormatString(config.UILog.Message, server: this.Server, player: player, target: target, geoData: geoData, steamData: steamData, reportReason: reportReason, chatChannel: chatChannel, msg: _msg);
                UILogOnServer(msg, config.UILog.Duration);
            }
            if (config.Announce is not null && config.Announce.Enabled && !string.IsNullOrWhiteSpace(config.Announce.Message)) {
                var msg = FormatString(config.Announce.Message, server: this.Server, player: player, target: target, geoData: geoData, steamData: steamData, reportReason: reportReason, chatChannel: chatChannel, msg: _msg);
                Announce(msg, config.Announce.Duration);
            }
        }
        #endregion

        #region Events
        public override void OnModulesLoaded() {
            Extensions.OnPlayerKicked += OnPlayerKicked;
            HandleEvent(Config.OnApiModulesLoaded);
        }

        public override Task OnConnected() {
            HandleEvent(Config.OnApiConnected);
            return Task.CompletedTask;
        }
        public override async Task OnPlayerConnected(RunnerPlayer player) {
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            HandleEvent(Config.OnPlayerConnected, player: player, geoData: GeoApi.GetData(player)?.Result);
        }
        public override Task<bool> OnPlayerTypedMessage(RunnerPlayer player, ChatChannel channel, string msg) {
            if (msg.StartsWith(Commands.CommandHandler.CommandConfiguration.CommandPrefix)) {
                HandleEvent(Config.OnPlayerChatCommand, player: player, chatChannel: channel, _msg: msg);
            } else {
                HandleEvent(Config.OnPlayerChatMessage, player: player, chatChannel: channel, _msg: msg);
            }
            return Task.FromResult(true);
        }
        private void OnPlayerKicked(RunnerPlayer player, string? reason) {
            HandleEvent(Config.OnPlayerKicked, player: player, _msg: reason!);
        }
        public override Task OnPlayerDisconnected(RunnerPlayer player) {
            HandleEvent(Config.OnPlayerDisconnected, player: player);
            return Task.CompletedTask;
        }
        public override Task OnPlayerReported(RunnerPlayer from, RunnerPlayer to, ReportReason reason, string additional) {
            HandleEvent(Config.OnPlayerReported, player: from, target: to, reportReason: reason, _msg: additional);
            return Task.CompletedTask;
        }
        public override Task OnDisconnected() {
            HandleEvent(Config.OnApiDisconnected);
            return Task.CompletedTask;
        }
        #region Enums
        public enum Duration {
            None,
            Short,
            Medium,
            Long,
            Infinite
        }
        #endregion
        #endregion

        #region Config
        public class LogConfigurationEntrySettings {
            public bool Enabled { get; set; } = false;
            public string Message { get; set; } = string.Empty;
            public Roles Roles { get; set; } = Roles.None;
            public Duration Duration { get; set; } = Duration.None;
        }
        public class DiscordWebhookLogConfigurationEntrySettings : LogConfigurationEntrySettings {
            public string WebhookUrl { get; set; } = string.Empty;
        }
        public class LogConfigurationEntry {
            public LogConfigurationEntrySettings Chat { get; set; } = null!;
            public LogConfigurationEntrySettings Console { get; set; } = null!;
            public LogConfigurationEntrySettings UILog { get; set; } = null!;
            public LogConfigurationEntrySettings Announce { get; set; } = null!;
            public LogConfigurationEntrySettings Modal { get; set; } = null!;
            public LogConfigurationEntrySettings File { get; set; } = null!;
            public DiscordWebhookLogConfigurationEntrySettings Discord { get; set; } = null!;
        }
        public Configuration Config { get; set; } = null!;
        public class Configuration : ModuleConfiguration {
            public string TimeStampFormat { get; set; } = "HH:mm:ss";
            public string TimeZone { get; set; } = System.TimeZone.CurrentTimeZone.StandardName;
            public Dictionary<string, string[]> randomReplacements = new Dictionary<string, string[]>() {
            { "joined", new string[] { "joined", "connected", "hailed" } },
        };
            public LogConfigurationEntry OnApiModulesLoaded { get; set; } = new LogConfigurationEntry() {
                Chat = new LogConfigurationEntrySettings() { Enabled = false, Message = "[{now}] API Modules loaded", Roles = Roles.Admin },
                Console = new LogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] API Modules loaded" },
                UILog = new LogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] API Modules loaded" },
                Discord = new DiscordWebhookLogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] API Modules loaded" },
            };
            public LogConfigurationEntry OnApiConnected { get; set; } = new LogConfigurationEntry() {
                Chat = new LogConfigurationEntrySettings() { Enabled = false, Message = "[{now}] Server connected to API", Roles = Roles.Admin },
                Console = new LogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] {server.str()} connected to API" },
                UILog = new LogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] Server connected to API" },
                Discord = new DiscordWebhookLogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] {server.str()} connected to API" },
            };
            public LogConfigurationEntry OnApiDisconnected { get; set; } = new LogConfigurationEntry() {
                Console = new LogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] Server disconnected from API", Roles = Roles.Admin },
                Discord = new DiscordWebhookLogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] Server disconnected from API" },
            };
            public LogConfigurationEntry OnPlayerConnected { get; set; } = new LogConfigurationEntry() {
                Chat = new LogConfigurationEntrySettings() { Enabled = false, Message = "[+] {player.Name} {random.joined} from {geoData.Country}", Roles = MoreRoles.All },
                Console = new LogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] [+] {player.Name} ({player.SteamID})) | {geoData.ToJson()}" },
                UILog = new LogConfigurationEntrySettings() { Enabled = true, Message = "{player.Name} [+]" },
                Discord = new DiscordWebhookLogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] `{player.str()}` connected from {geoData.Country} :flag_{geoData.CountryCode}:" },
            };
            public LogConfigurationEntry OnPlayerDisconnected { get; set; } = new LogConfigurationEntry() {
                Chat = new LogConfigurationEntrySettings() { Enabled = false, Message = "[-] {player.Name} left", Roles = MoreRoles.All },
                Console = new LogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] [-] {player.Name} ({player.SteamID})) [{player.IP}]" },
                UILog = new LogConfigurationEntrySettings() { Enabled = true, Message = "{player.Name} [-]" },
                Discord = new DiscordWebhookLogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] `{player.str()}` disconnected :arrow_left:" },
            };
            public LogConfigurationEntry OnPlayerKicked { get; set; } = new LogConfigurationEntry() {
                Chat = new LogConfigurationEntrySettings() { Enabled = false, Message = "[-] {player.Name} was kicked for {msg}", Roles = MoreRoles.All },
                Console = new LogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] [-] {player.Name} ({player.SteamID})) [{player.IP}] kicked for {msg}" },
                Discord = new DiscordWebhookLogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] `{player.str()}` kicked :leg:" },
            };
            public LogConfigurationEntry OnPlayerChatMessage { get; set; } = new LogConfigurationEntry() {
                Console = new LogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] {player.str()} says \"{msg}\" in {chatChannel}" },
                Discord = new DiscordWebhookLogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] `{player.Name}` says \"{msg}\" in {chatChannel} :speech_balloon:" },
            };
            public LogConfigurationEntry OnPlayerChatCommand { get; set; } = new LogConfigurationEntry() {
                Console = new LogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] {player.str()} issued command \"{msg}\" in {chatChannel}" },
                Discord = new DiscordWebhookLogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] `{player.Name}` issued command \"{msg}\" in {chatChannel}" },
            };
            public LogConfigurationEntry OnPlayerReported { get; set; } = new LogConfigurationEntry() {
                Chat = new LogConfigurationEntrySettings() { Enabled = false, Message = "{target.Name} was reported for {reason}", Roles = MoreRoles.All },
                Console = new LogConfigurationEntrySettings() { Enabled = true, Message = "[{now}] {player.str()} reported {target.str()} for {reason}: \"{msg}\"" },
                UILog = new LogConfigurationEntrySettings() { Enabled = true, Message = "{target.Name} was reported ({reason})" },
                Modal = new LogConfigurationEntrySettings() { Enabled = false, Message = "{target.fullstr()}\nwas reported by\n{player.fullstr()}\n\nReason: {reason}\n\n\"{msg}\"", Roles = MoreRoles.Staff },
                Discord = new DiscordWebhookLogConfigurationEntrySettings() { Enabled = false, Message = "[{now}] {target.Name} was reported for {reason} :warning:" },
            };
        }
        #endregion
    }
}