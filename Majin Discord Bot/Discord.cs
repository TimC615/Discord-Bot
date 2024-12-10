using Discord.WebSocket;
using Discord;
using System.Collections.ObjectModel;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Api.Services;
using TwitchLib.Api.Services.Events.LiveStreamMonitor;
using TwitchLib.Api.Services.Events;
using static System.Formats.Asn1.AsnWriter;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using NHttp;
using System.Linq;
using TwitchLib.Api.Helix;
using FishyFlip;
using Microsoft.Extensions.Logging.Debug;
using System.Threading;
using FishyFlip.Models;
using Newtonsoft.Json;

namespace Majin_Discord_Bot
{
    //barebones skeleton retreived from:
    //https://www.youtube.com/watch?v=Y76B0rAoZpo&ab_channel=HassanHabib
    //https://github.com/hassanhabib/Discord-Dot-Net-Bot



    //https://github.com/TwitchLib/TwitchLib.Api


    //used to convert bluesky did to handle
    public class BlueskyDidToHandleAPI
    {
        [JsonProperty("@context")]
        public List<string> Context { get; set; }
        public string Id { get; set; }
        public List<string> AlsoKnownAs { get; set; }
        public List<VerificationMethod> VerificationMethod { get; set; }
        public List<Service> Service { get; set; }
    }

    public class Service
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string ServiceEndpoint { get; set; }
    }

    public class VerificationMethod
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Controller { get; set; }
        public string PublicKeyMultibase { get; set; }
    }


    internal class Discord
    {
        private DiscordSocketClient discord;
        private const string token = "MTMxMDcxMjA4MjM0MTAzNjE0Mw.GWAnLb.Caoy3zCELs3uz_oi0ApGY4jWhflS8ccl2wBdtU";
        private List<SocketGuild> guilds;
        private SocketGuild[] guildsArr;

        private TwitchAPI twitchAPI;
        private LiveStreamMonitorService monitor;
        private readonly string twitchClientId = "oxv4prt4vrqed9egy96up9z8oyeq46";
        private readonly string twitchClientSecret = "2adfs7nowo2dvemnmq0zq0egi0xhe8";
        private readonly string twitchRedirectUri = "http://localhost:3000";
        private string twitchAccessToken;
        private string twitchRefreshToken;

        HttpServer WebServer;


        //-----------------------------------------------------------------------------------------------------------------
        //                                                  Discord
        //-----------------------------------------------------------------------------------------------------------------

        public Discord()
        {
            Console.WriteLine("Discord start");
            this.discord = new DiscordSocketClient();
            this.discord.MessageReceived += MessageHandler;
        }

        public async Task StartBotAsync()
        {

            this.discord.Log += LogFuncAsync;
            await this.discord.LoginAsync(TokenType.Bot, token);

            await this.discord.StartAsync();

            //ConnectToTwitchAPI();
            Twitch twitch = new Twitch();
            //Twitch-specific Events
            twitch.OnChannelWentLive += Twitch_OnStreamWentLive;

            twitch.ConnectToTwitchAPI();


            
            Bluesky bluesky = new Bluesky();
            //Bluesky-specific Events
            bluesky.OnNewPost += Bluesky_OnNewPost;

            bluesky.ConnectToBluesky();

            
            await Task.Delay(-1);

            async Task LogFuncAsync(LogMessage message) =>
                Console.WriteLine(DateTime.Now + "\t" + message.ToString());
        }

        private void Twitch_OnStreamWentLive(object? sender, OnStreamOnlineArgs e)
        {
            string wentLiveMessage = $"{e.Stream.UserName} just went live playing {e.Stream.GameName}\nhttps://www.twitch.tv/{e.Stream.UserLogin}";
            Console.WriteLine("Event Handler: " + wentLiveMessage);

            //SendDiscordMessageToServer(1310713824814567475, 1310713827037544531, wentLiveMessage);    //general text channel
            SendDiscordMessageToServer(1310713824814567475, 1314363693773099009, wentLiveMessage);      //spam text channel
            
        }

        private async void Bluesky_OnNewPost(object? sender, ATWebSocketRecord e)
        {
            Console.WriteLine("Discord: Detected new Bluesky post");
            string handle = "";

            HttpClient BlueskyDIDToHandleConnection = new HttpClient();
            BlueskyDIDToHandleConnection.BaseAddress = new Uri("https://plc.directory");
            var response = await BlueskyDIDToHandleConnection.GetAsync($"/{e.Did}");

            if (response.IsSuccessStatusCode)
            {
                string stringResponse = await response.Content.ReadAsStringAsync();
                BlueskyDidToHandleAPI result = JsonConvert.DeserializeObject<BlueskyDidToHandleAPI>(stringResponse);

                if (result == null)
                {
                    Console.WriteLine($"Couldn't find handle for did: {e.Did}");
                }
                else
                {
                    handle = result.AlsoKnownAs.ToArray()[0].Split('/')[2];
                    Console.WriteLine($"IT WORKED\t\tDid: {e.Did}\tHandle: {handle}");

                    string wentLiveMessage = $"Hey there's a new bluesky post!\nhttps://bsky.app/profile/{handle}/post/{e.Commit.RKey}";
                    Console.WriteLine("Event Handler: " + wentLiveMessage);

                    SendDiscordMessageToServer(1310713824814567475, 1314363693773099009, wentLiveMessage);
                }
            }
            else
            {
                Console.WriteLine($"Response code not successful for getting handle from: {e.Did}\t Reason: {response.StatusCode}");
            }
        }

        private async Task MessageHandler(SocketMessage message)
        {
            if (message.Author.IsBot) return;


            //await ReplyAsync(message, "C# response works!");

            guildsArr = discord.Guilds.ToArray();

            foreach (SocketGuild guild in guildsArr)
            {
                Console.WriteLine(DateTime.Now + "\tGuild: {0}\tID: {1}", guild.Name, guild.Id);
            }

            //CheckIfTwitchIsLive();

            SendDiscordMessageToServer(1310713824814567475, 1314363693773099009, "This is a test");

            //await discord.GetGuild(1310713824814567475).GetTextChannel(1310713827037544531).SendMessageAsync("This is a test");

            //discord.GetGuild().GetTextChannel().SendMessageAsync("This is a test"); ;
        }

        private async Task SendDiscordMessageToServer(ulong guildNum, ulong channelNum, string message)
        {
            await discord.GetGuild(guildNum).GetTextChannel(channelNum).SendMessageAsync(message);
            
        }

        private async Task ReplyAsync(SocketMessage message, string response) =>
            await message.Channel.SendMessageAsync(response);


        static void Main(string[] args) =>
            new Discord().StartBotAsync().GetAwaiter().GetResult();
    }
}
