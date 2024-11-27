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

namespace Majin_Discord_Bot
{
    //barebones skeleton retreived from:
    //https://www.youtube.com/watch?v=Y76B0rAoZpo&ab_channel=HassanHabib
    //https://github.com/hassanhabib/Discord-Dot-Net-Bot



    //https://github.com/TwitchLib/TwitchLib.Api
    internal class Program
    {
        private readonly DiscordSocketClient discord;
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

        public Program()
        {
            this.discord = new DiscordSocketClient();
            this.discord.MessageReceived += MessageHandler;
        }

        public async Task StartBotAsync()
        {

            this.discord.Log += LogFuncAsync;
            await this.discord.LoginAsync(TokenType.Bot, token);

            ConnectToTwitchAPI();


            await this.discord.StartAsync();
            await Task.Delay(-1);

            async Task LogFuncAsync(LogMessage message) =>
                Console.WriteLine(message.ToString());
        }

        private void TestMethod()
        {
            Console.WriteLine("This is a test");
        }

        private async Task MessageHandler(SocketMessage message)
        {
            if (message.Author.IsBot) return;


            await ReplyAsync(message, "C# response works!");
            Console.WriteLine(discord.Guilds.Count.ToString());
            Console.WriteLine(discord.Guilds);

            guildsArr = discord.Guilds.ToArray();

            foreach (SocketGuild guild in guildsArr)
            {
                Console.WriteLine("Guild: {0}\tID: {1}", guild.Name, guild.Id);
            }

            CheckIfTwitchIsLive();

            
            await discord.GetGuild(1310713824814567475).GetTextChannel(1310713827037544531).SendMessageAsync("This is a test");

            //discord.GetGuild().GetTextChannel().SendMessageAsync("This is a test"); ;
        }




        private async Task CheckIfTwitchIsLive()
        {
            List<string> testString = new List<string>();
            testString.Add("SmallAnt");

            GetStreamsResponse response = twitchAPI.Helix.Streams.GetStreamsAsync(userLogins: testString).Result;

            Console.WriteLine(response.ToString());

            if (response.Streams.Count() > 0)
            {
                Console.WriteLine("Stream live " + response.Streams[0].Title);
            }
            else
            {
                Console.WriteLine("Stream not live");
            }

        }

        private async Task ConfigLiveMonitorAsync()
        {
            Console.WriteLine("ConfigLiveMonitorAsync() Triggered");
            /*
            var authUrl = "https://id.twitch.tv/oauth2/authorize?response_type=code&client_id=" +
                twitchAPI.Settings.ClientId + "&redirect_uri=" + twitchRedirectUri;
            // + "&scope=" + String.Join("+", Scopes)

            //launch the above authUrl to connect to twitch, allow permissions, and start the proces of retrieving auth tokens
            try
            {
                ProcessStartInfo twitchAuthBrowser = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = authUrl
                };
                Process.Start(twitchAuthBrowser);
            }
            catch (Exception ex)
            {
                Console.WriteLine("StartBot Open URL Error: " + ex.Message);
            }
            */



            
            //twitchAPI.Settings.AccessToken = "";

            monitor = new LiveStreamMonitorService(twitchAPI, 10);

            monitor.OnStreamOnline += Monitor_OnStreamOnline;
            monitor.OnStreamOffline += Monitor_OnStreamOffline;
            monitor.OnStreamUpdate += Monitor_OnStreamUpdate;
            monitor.OnServiceStarted += Monitor_OnServiceStarted;

            monitor.OnServiceStarted += Monitor_OnServiceStarted;
            monitor.OnChannelsSet += Monitor_OnChannelsSet;

           

            List<string> channelLoginsToWatch = new List<string>();
            channelLoginsToWatch.Add("Yogscast");
            channelLoginsToWatch.Add("DashDucks");

            List<string> channelIdsToWatch = new List<string>();
            foreach (var user in twitchAPI.Helix.Users.GetUsersAsync(logins: channelLoginsToWatch).Result.Users)
            {
                channelIdsToWatch.Add(user.Id);
            }

            //channelIdsToWatch.Add(twitchAPI.Helix.Users.GetUsersAsync(logins:));
            monitor.SetChannelsById(channelIdsToWatch);

            monitor.Start(); //Keep at the end!

            await Task.Delay(-1);
        }

        private void Monitor_OnServiceOnline(object sender, OnServiceStartedArgs e)
        {
            Console.WriteLine("Monitor Service Started");
        }

        async private void Monitor_OnStreamOnline(object sender, OnStreamOnlineArgs e)
        {
            Console.WriteLine("Stream Started: " + e.Stream.Title);

            await discord.GetGuild(1310713824814567475).GetTextChannel(1310713827037544531).SendMessageAsync($"{e.Stream.UserName} just went live playing {e.Stream.GameName}.\nhttps://www.twitch.tv/{e.Stream.UserLogin}");
        }

        private void Monitor_OnStreamUpdate(object sender, OnStreamUpdateArgs e)
        {
            Console.WriteLine("Stream Update: " + e.Stream.Title);
        }

        private void Monitor_OnStreamOffline(object sender, OnStreamOfflineArgs e)
        {
            Console.WriteLine("Stream Offline: " + e.Stream.UserName);
        }

        private void Monitor_OnChannelsSet(object sender, OnChannelsSetArgs e)
        {
            foreach(var setChannel in e.Channels)
            {
                Console.WriteLine("Channel monitoring set for " + setChannel.ToString());
            }
        }

        private void Monitor_OnServiceStarted(object sender, OnServiceStartedArgs e)
        {
            Console.WriteLine("Monitor Started");
        }



        void ConnectToTwitchAPI()
        {
            InitializeTwitchWebServer();

            twitchAPI = new TwitchAPI();
            twitchAPI.Settings.ClientId = twitchClientId;
            twitchAPI.Settings.Secret = twitchClientSecret;

            var authUrl = "https://id.twitch.tv/oauth2/authorize?response_type=code&client_id=" +
                twitchAPI.Settings.ClientId + "&redirect_uri=" + twitchRedirectUri;
            // + "&scope=" + String.Join("+", Scopes)

            //launch the above authUrl to connect to twitch, allow permissions, and start the proces of retrieving auth tokens
            try
            {
                ProcessStartInfo twitchAuthBrowser = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = authUrl
                };
                Process.Start(twitchAuthBrowser);
            }
            catch (Exception ex)
            {
                Console.WriteLine("StartBot Open URL Error: " + ex.Message);
            }
        }

        void InitializeTwitchWebServer()
        {
            Console.WriteLine("InitializeTwitchWebServer() Triggered");

            //Create local web server (allows for requesting OAUTH token)
            WebServer = new HttpServer();

            //int port = GetFreePort();
            //WebServer.EndPoint = new IPEndPoint(IPAddress.Loopback, port);

            WebServer.EndPoint = new IPEndPoint(IPAddress.Loopback, 3000);  //must specify on twitch dev console to use "http://localhost:3000"
            //WebServer.EndPoint = new IPEndPoint(IPAddress.Loopback, 80);  //defaults to this if no port specified in twitch dev console

            WebServer.RequestReceived += async (s, e) =>
            {
                using (var writer = new StreamWriter(e.Response.OutputStream))
                {
                    if (e.Request.QueryString.AllKeys.Any("code".Contains))
                    {
                        //initialize base TwitchLib API
                        var code = e.Request.QueryString["code"];
                        var ownerOfChannelAccessAndRefresh = await GetAccessAndRefreshTokens(code);

                        twitchAccessToken = ownerOfChannelAccessAndRefresh.Item1; //access token
                        twitchRefreshToken = ownerOfChannelAccessAndRefresh.Item2; //refresh token

                        //SetNameAndIdByOauthedUser(CachedOwnerOfChannelAccessToken).Wait();
                        //InitializeOwnerOfChannelConnection(TwitchChannelName, twitchAccessToken);
                        //InitializeTwitchAPI(twitchAccessToken);
                        twitchAPI.Settings.AccessToken = twitchAccessToken;

                        ConfigLiveMonitorAsync();
                    }
                }
            };

            WebServer.Start();
            Console.WriteLine("Web server started on: " + WebServer.EndPoint);
        }

        async Task<Tuple<String, String>> GetAccessAndRefreshTokens(string code)
        {
            Console.WriteLine("GetAccessAndRefreshTokens() Triggered");

            HttpClient client = new HttpClient();
            var values = new Dictionary<string, String>
            {
                { "client_id", twitchClientId },
                { "client_secret", twitchClientSecret },
                { "code", code },
                { "grant_type", "authorization_code" },
                { "redirect_uri", twitchRedirectUri }
            };

            var content = new FormUrlEncodedContent(values);

            var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", content);

            var responseString = await response.Content.ReadAsStringAsync();

            var json = JObject.Parse(responseString);

            return new Tuple<string, string>(json["access_token"].ToString(), json["refresh_token"].ToString());
        }

        private async Task ReplyAsync(SocketMessage message, string response) =>
            await message.Channel.SendMessageAsync(response);

        static void Main(string[] args) =>
            new Program().StartBotAsync().GetAwaiter().GetResult();
    }
}
