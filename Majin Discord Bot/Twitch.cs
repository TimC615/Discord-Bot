using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Api.Services;
using TwitchLib.Api;
using Newtonsoft.Json.Linq;
using NHttp;
using System.Diagnostics;
using System.Net;
using static System.Runtime.InteropServices.JavaScript.JSType;
using TwitchLib.Api.Services.Events.LiveStreamMonitor;
using TwitchLib.Api.Services.Events;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace Majin_Discord_Bot
{
    internal class Twitch
    {
        private TwitchAPI twitchAPI;
        private LiveStreamMonitorService monitor;
        private readonly string twitchClientId = "oxv4prt4vrqed9egy96up9z8oyeq46";
        private readonly string twitchClientSecret = "2adfs7nowo2dvemnmq0zq0egi0xhe8";
        private readonly string twitchRedirectUri = "http://localhost:3000";
        private string twitchAccessToken;
        private string twitchRefreshToken;
        HttpServer WebServer;

        public event EventHandler<OnStreamOnlineArgs> OnChannelWentLive;

        //old testing code (might be useful??)
        /*
        private async Task CheckIfTwitchIsLive()
        {
            List<string> testString = new List<string>();
            testString.Add("SmallAnt");

            GetStreamsResponse response = twitchAPI.Helix.Streams.GetStreamsAsync(userLogins: testString).Result;

            Console.WriteLine(DateTime.Now + "\t" + response.ToString());

            if (response.Streams.Count() > 0)
            {
                Console.WriteLine(DateTime.Now + "\tStream live " + response.Streams[0].Title);
            }
            else
            {
                Console.WriteLine(DateTime.Now + "\tStream not live");
            }

        }
        */

        public void ConnectToTwitchAPI()
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
                Console.WriteLine(DateTime.Now + "\tStartBot Open URL Error: " + ex.Message);
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
            Console.WriteLine(DateTime.Now + "\tWeb server started on: " + WebServer.EndPoint);
        }

        async Task<Tuple<string, string>> GetAccessAndRefreshTokens(string code)
        {
            Console.WriteLine("GetAccessAndRefreshTokens() Triggered");

            HttpClient client = new HttpClient();
            var values = new Dictionary<string, string>
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

        private async Task ConfigLiveMonitorAsync()
        {
            Console.WriteLine("ConfigLiveMonitorAsync() Triggered");

            monitor = new LiveStreamMonitorService(twitchAPI, 10);

            monitor.OnServiceStarted += Monitor_OnServiceStarted;
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
            Console.WriteLine(DateTime.Now + "\tMonitor Service Started");

        }

        private void Monitor_OnStreamOnline(object sender, OnStreamOnlineArgs e)
        {
            Console.WriteLine(DateTime.Now + "\tStream Started: " + e.Stream.Title);
            OnChannelWentLive?.Invoke(this, e);
            //await discord.GetGuild(1310713824814567475).GetTextChannel(1310713827037544531).SendMessageAsync($"{e.Stream.UserName} just went live playing {e.Stream.GameName}.\nhttps://www.twitch.tv/{e.Stream.UserLogin}");
        }

        private void Monitor_OnStreamUpdate(object sender, OnStreamUpdateArgs e)
        {
            //Console.WriteLine(DateTime.Now + "\tStream Update: " + e.Stream.Title);
        }

        private void Monitor_OnStreamOffline(object sender, OnStreamOfflineArgs e)
        {
            Console.WriteLine(DateTime.Now + "\tStream Offline: " + e.Stream.UserName);
        }

        private void Monitor_OnChannelsSet(object sender, OnChannelsSetArgs e)
        {
            foreach (var setChannel in e.Channels)
            {
                Console.WriteLine(DateTime.Now + "\tChannel monitoring set for " + setChannel.ToString());
            }
        }

        private void Monitor_OnServiceStarted(object sender, OnServiceStartedArgs e)
        {
            Console.WriteLine(DateTime.Now + "\tMonitor Started");
        }
    }
}
