using Discord.WebSocket;
using Discord;
using Discord.Interactions;
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
using static System.Runtime.InteropServices.JavaScript.JSType;
using TwitterSharp.Response.RTweet;
using Microsoft.Extensions.Configuration;
using static System.Net.Mime.MediaTypeNames;
using System.Configuration;

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

    public class DiscordNotificationInformation
    {
        public ulong guildNum { get; }
        public ulong channelNum { get; }

        public DiscordNotificationInformation(ulong guildNum, ulong channelNum)
        {
            this.guildNum = guildNum;
            this.channelNum = channelNum;
        }
    }

    internal class Discord
    {
        private IConfiguration config;

        private DiscordSocketClient discord;
        private List<SocketGuild> guilds;
        private SocketGuild[] guildsArr;

        /*
        private TwitchAPI twitchAPI;
        private LiveStreamMonitorService monitor;
        private readonly string twitchClientId = "";
        private readonly string twitchClientSecret = "";
        private readonly string twitchRedirectUri = "http://localhost:3000";
        private string twitchAccessToken;
        private string twitchRefreshToken;
        */


        HttpServer WebServer;

        Twitch twitch;
        Twitter twitter;
        Bluesky bluesky;



        //-----------------------------------------------------------------------------------------------------------------
        //                                                  Discord
        //-----------------------------------------------------------------------------------------------------------------

        public Discord()
        {
            Console.WriteLine($"{DateTime.Now}\t Connecting to Discord...");
            this.discord = new DiscordSocketClient();
            //this.discord.MessageReceived += Discord_MessageHandler;   //use if bot needs to respond to user messages
            this.discord.Connected += Discord_Connected;
            this.discord.ReactionAdded += Discord_ReactionAdded;
            this.discord.ReactionRemoved += Discord_ReactionRemoved;
        }

        static void Main(string[] args) =>
            new Discord().StartBotAsync().GetAwaiter().GetResult();

        public async Task StartBotAsync()   //connects to discord first. all other API connections occur once discord is successfully connected to
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<Discord>();

            config = configBuilder.Build();

            this.discord.Log += LogFuncAsync;
            await this.discord.LoginAsync(TokenType.Bot, config.GetValue<string>("Discord:Token"));

            await this.discord.StartAsync();

            await Task.Delay(-1);

            async Task LogFuncAsync(LogMessage message) =>
                Console.WriteLine(DateTime.Now + "\t" + message.ToString());
        }

        private async Task Discord_Connected()
        {
            Console.WriteLine($"{DateTime.Now}\tConnected to Discord");

            //ConnectToTwitchAPI();
            twitch = new Twitch();
            //Twitch-specific Events
            twitch.OnChannelWentLive += Twitch_OnStreamWentLive;

            twitch.ConnectToTwitchAPI(config);



            bluesky = new Bluesky();
            //Bluesky-specific Events
            bluesky.OnNewPost += Bluesky_OnNewPost;

            bluesky.ConnectToBluesky();


            twitter = new Twitter();
            //Twitte-specific Events
            twitter.OnNewPost += Twitter_OnNewPost;

            twitter.ConnectToTwitter(config);   //TwitterSharp
        }


        //--------------------------------------------------------------------------------------------------------------
        //                                          Discord Role Manager
        //--------------------------------------------------------------------------------------------------------------

        private async Task Discord_ReactionAdded(Cacheable<IUserMessage, ulong> userMessage, Cacheable<IMessageChannel, ulong> messageChannel, SocketReaction reaction)
        {
            try
            {
                IRole roleToAdd;

                if (!messageChannel.HasValue)
                    throw new Exception("messageChannel does not have value assigned");
                if (reaction.User.Value == null)
                    throw new Exception("reaction.User does not have value assigned");

                ulong roleHandlerMessageId = GetRoleHandlerMessageId();
                if (roleHandlerMessageId == 0)
                    throw new Exception("issue when reading from stored roleHandlerMessageId");

                if (userMessage.Id != roleHandlerMessageId)
                    return;

                //to make scalable, put in an array into appsettings.json containing <string, ulong> pairs of emote name and associated role id
                //this implementation only works with emotes (emoticons do not return an IEmote object)
                switch (reaction.Emote.Name)
                {
                    case "thecak12Wave":
                        roleToAdd = (messageChannel.Value as SocketGuildChannel).Guild.GetRoleAsync(1316846547249401967).Result;  //test role 1
                        await (reaction.User.Value as SocketGuildUser).AddRoleAsync(roleToAdd);

                        Console.WriteLine($"{DateTime.Now}\tRole {roleToAdd.Name} added to user {reaction.User.Value.Username}");
                        break;
                    case "thecak12Sad":
                        roleToAdd = (messageChannel.Value as SocketGuildChannel).Guild.GetRoleAsync(1316846619055882293).Result;  //test role 1
                        await (reaction.User.Value as SocketGuildUser).AddRoleAsync(roleToAdd);

                        Console.WriteLine($"{DateTime.Now}\tRole {roleToAdd.Name} added to user {reaction.User.Value.Username}");
                        break;
                    case "thecak12HYPE":
                        roleToAdd = (messageChannel.Value as SocketGuildChannel).Guild.GetRoleAsync(1316846643068403835).Result;  //test role 1
                        await (reaction.User.Value as SocketGuildUser).AddRoleAsync(roleToAdd);

                        Console.WriteLine($"{DateTime.Now}\tRole {roleToAdd.Name} added to user {reaction.User.Value.Username}");
                        break;
                }
            }
            catch (Exception except)
            {
                Console.WriteLine($"{DateTime.Now}\tDiscord_ReactionAdded Error: {except.Message}");
                return;
            }
        }

        private async Task Discord_ReactionRemoved(Cacheable<IUserMessage, ulong> userMessage, Cacheable<IMessageChannel, ulong> messageChannel, SocketReaction reaction)
        {
            try
            {
                IRole roleToRemove;

                if (!messageChannel.HasValue)
                    throw new Exception("messageChannel does not have value assigned");
                if (reaction.User.Value == null)
                    throw new Exception("reaction.User does not have value assigned");

                ulong roleHandlerMessageId = GetRoleHandlerMessageId();
                if (roleHandlerMessageId == 0)
                    throw new Exception("issue when reading from stored roleHandlerMessageId");

                if (userMessage.Id != roleHandlerMessageId)
                    return;

                if (userMessage.Id != roleHandlerMessageId)
                    return;

                switch (reaction.Emote.Name)
                {
                    case "thecak12Wave":
                        roleToRemove = (messageChannel.Value as SocketGuildChannel).Guild.GetRoleAsync(1316846547249401967).Result;  //test role 1
                        await (reaction.User.Value as SocketGuildUser).RemoveRoleAsync(roleToRemove);

                        Console.WriteLine($"{DateTime.Now}\tRole {roleToRemove.Name} removed user {reaction.User.Value.Username}");
                        break;
                    case "thecak12Sad":
                        roleToRemove = (messageChannel.Value as SocketGuildChannel).Guild.GetRoleAsync(1316846619055882293).Result;  //test role 1
                        await (reaction.User.Value as SocketGuildUser).RemoveRoleAsync(roleToRemove);

                        Console.WriteLine($"{DateTime.Now}\tRole {roleToRemove.Name} removed from user {reaction.User.Value.Username}");
                        break;
                    case "thecak12HYPE":
                        roleToRemove = (messageChannel.Value as SocketGuildChannel).Guild.GetRoleAsync(1316846643068403835).Result;  //test role 1
                        await (reaction.User.Value as SocketGuildUser).RemoveRoleAsync(roleToRemove);

                        Console.WriteLine($"{DateTime.Now}\tRole {roleToRemove.Name} removed user {reaction.User.Value.Username}");
                        break;
                }
            }
            catch (Exception except)
            {
                Console.WriteLine($"{DateTime.Now}\tDiscord_ReactionRemoved Error: {except.Message}");
                return;
            }
        }


        //--------------------------------------------------------------------------------------------------------------
        //                                          New Socials Activity
        //--------------------------------------------------------------------------------------------------------------

        private void Twitch_OnStreamWentLive(object? sender, OnStreamOnlineArgs e)
        {
            string wentLiveMessage = $"{e.Stream.UserName} just went live playing {e.Stream.GameName}\nhttps://www.twitch.tv/{e.Stream.UserLogin}";
            Console.WriteLine($"{DateTime.Now}\t{wentLiveMessage}");

            DiscordNotificationInformation notificationInfo = GetDiscordNotificationInfo();
            SendDiscordMessageToServer(notificationInfo.guildNum, notificationInfo.channelNum, wentLiveMessage);

        }

        private async void Bluesky_OnNewPost(object? sender, ATWebSocketRecord e)
        {
            //Console.WriteLine("Discord: Detected new Bluesky post");
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
                    Console.WriteLine($"{DateTime.Now}\tCouldn't find handle for did: {e.Did}");
                }
                else
                {
                    handle = result.AlsoKnownAs.ToArray()[0].Split('/')[2];

                    string newBlueskyPost = $"Hey there's a new bluesky post!\nhttps://bsky.app/profile/{handle}/post/{e.Commit.RKey}";
                    Console.WriteLine($"{DateTime.Now}\t{newBlueskyPost}");

                    //SendDiscordMessageToServer(1310713824814567475, 1314363693773099009, newBlueskyPost);

                    DiscordNotificationInformation notificationInfo = GetDiscordNotificationInfo();
                    SendDiscordMessageToServer(notificationInfo.guildNum, notificationInfo.channelNum, newBlueskyPost);
                }
            }
            else
            {
                Console.WriteLine($"{DateTime.Now}\tResponse code not successful for getting handle from: {e.Did}\t Reason: {response.StatusCode}");
            }
        }

        private void Twitter_OnNewPost(object? sender, TwitterNewPostResponse e)
        {
            string postUrl = $"https://x.com/{e.Username}/status/{e.PostId}";
            string newPostMessage = $"Hey there's a new Twitter post!\n{postUrl}";
            //SendDiscordMessageToServer(1310713824814567475, 1314363693773099009, newPostMessage);

            DiscordNotificationInformation notificationInfo = GetDiscordNotificationInfo();
            SendDiscordMessageToServer(notificationInfo.guildNum, notificationInfo.channelNum, newPostMessage);
        }


        //--------------------------------------------------------------------------------------------------------------
        //                                          Utility Methods
        //--------------------------------------------------------------------------------------------------------------


        private async Task Discord_MessageHandler(SocketMessage message)
        {
            if (message.Author.IsBot) return;

            if(message.Author.Id == 316081147463598081) //triggers for user: "TheCakeIsAPie"
            {
                await ReplyAsync(message, $"{DateTime.Now} C# response works!");

                guildsArr = discord.Guilds.ToArray();

                foreach (SocketGuild guild in guildsArr)
                {
                    Console.WriteLine(DateTime.Now + "\tGuild: {0}\tID: {1}", guild.Name, guild.Id);
                }
            }
            //CheckIfTwitchIsLive();

            //SendDiscordMessageToServer(1310713824814567475, 1314363693773099009, "This is a test");

            //await discord.GetGuild(1310713824814567475).GetTextChannel(1310713827037544531).SendMessageAsync("This is a test");

            //discord.GetGuild().GetTextChannel().SendMessageAsync("This is a test"); ;
        }

        private async Task SendDiscordMessageToServer(ulong guildNum, ulong channelNum, string message)
        {
            await discord.GetGuild(guildNum).GetTextChannel(channelNum).SendMessageAsync(message);
        }

        private DiscordNotificationInformation GetDiscordNotificationInfo()
        {
            ulong serverId;
            ulong channelId;

            try
            {
                serverId = ulong.Parse(config.GetValue<string>("Discord:NotificationServerID"));
                channelId = ulong.Parse(config.GetValue<string>("Discord:NotificationChannelID"));
            }
            catch (Exception except)
            {
                Console.WriteLine($"{DateTime.Now}\tError parsing Discord notification info to ulong: {except.Message}");
                return null;
            }


            DiscordNotificationInformation info = new DiscordNotificationInformation(serverId, channelId);
            return info;
        }

        private ulong GetRoleHandlerMessageId()
        {
            ulong roleHandlerMessageId;
            try
            {
                roleHandlerMessageId = ulong.Parse(config.GetValue<string>("Discord:RoleHandlerMessageId"));
            }
            catch (Exception except)
            {
                Console.WriteLine($"{DateTime.Now}\tError converting stored role handler message Id to ulong: {except.Message}");
                return 0;
            }

            return roleHandlerMessageId;
        }

        private async Task ReplyAsync(SocketMessage message, string response) =>
            await message.Channel.SendMessageAsync(response);
    }
}
