using FishyFlip.Models;
using FishyFlip;
using Microsoft.Extensions.Logging.Debug;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FishyFlip.Tools;
using System.Security.Cryptography;
using Newtonsoft.Json;
using FishyFlip.Events;
using TwitchLib.Api.Services.Events.LiveStreamMonitor;

namespace Majin_Discord_Bot
{
    public class HandleResolverAPI
    {
        public string did { get; set; }
    }

    //not used yet
    /*
    //used to handle de-serialization of JetStream JSON messages
    public class JetStreamRawMessage
    {
        public string did { get; set; }
        public long time_us { get; set; }
        public string kind { get; set; }
        public JetStreamCommit commit { get; set; }
    }

    public class JetStreamCommit
    {
        public string rev { get; set; }
        public string operation { get; set; }
        public string collection { get; set; }
        public string rkey { get; set; }
    }
    */

    internal class Bluesky
    {
        //-----------------------------------------------------------------------------------------------------------------
        //                                                  Bluesky
        //-----------------------------------------------------------------------------------------------------------------

        public event EventHandler<ATWebSocketRecord> OnNewPost;

        public async void ConnectToBluesky()
        {
            var debugLog = new DebugLoggerProvider();

            //WebProtocol looks to handle connection data
            // You can set a custom url with WithInstanceUrl
            var atWebProtocolBuilder = new ATWebSocketProtocolBuilder()
                .WithLogger(debugLog.CreateLogger("ATWebSocketProtocolBuilder Feed"));
            var atWebProtocol = atWebProtocolBuilder.Build();

            //Protocol looks to handle messages from firehose (or other soruces)
            //atProtocolBuilder handles actual actions from firehose (messages, likes, reposts, etc)
            var atProtocolBuilder = new ATProtocolBuilder()
                .WithLogger(debugLog.CreateLogger("ATProtocolBuilder Feed"));
            //.WithSessionRefreshInterval(TimeSpan.FromSeconds(60));
            var atProtocol = atProtocolBuilder.Build();


            //hook into firehose
            /*
            atWebProtocol.OnSubscribedRepoMessage += (sender, args) =>
            {
                Task.Run(() => HandleMessageAsync(args.Message, atProtocol)).FireAndForget();
            };

            //await atWebProtocol.StartSubscribeReposAsync();
            //var key = Console.ReadKey();
            //await atWebProtocol.StopSubscriptionAsync();
            */


            //hook into JetStream (firehose with filterable variables for events received)
            var atWebSocketProtocol = new ATWebSocketProtocol(atProtocol);

            ATJetStreamBuilder atJetStreamBuilder = new ATJetStreamBuilder()
                .WithLogger(debugLog.CreateLogger("ATJetStreamBuilder Feed"));
            atJetStreamBuilder.Build();

            ATJetStreamOptions atJetStreamOptions = new ATJetStreamOptions();   //needed to create new JetStream object (basically just needed to set url variable)
            ATJetStream atJetStream = new ATJetStream(atJetStreamOptions);

            atJetStream.OnConnectionUpdated += (sender, args) =>
            {
                Task.Run(() => JetStream_ConnectionUpdated(args).FireAndForget());
            };


            atJetStream.OnRawMessageReceived += (sender, args) =>
            {
                //Task.Run(() => JetStream_RawMessageReceived(args).FireAndForget());
            };


            atJetStream.OnRecordReceived += (sender, args) =>
            {
                Task.Run(() => JetStream_RecordReceived(args.Record).FireAndForget());
            };

            string[] wantedCollections = { "app.bsky.feed.post" };

            List<string> wantedHandles = new List<string>();
            
            wantedHandles.Add("majinorca.bsky.social");
            

            List<string> wantedDids = new List<string>();

            HttpClient BlueskyAPIHandleResolveConnection = new HttpClient();
            BlueskyAPIHandleResolveConnection.BaseAddress = new Uri("https://bsky.social/xrpc/");

            for (int x = 0; x < wantedHandles.Count; x++)
            {
                string apiUrl = "com.atproto.identity.resolveHandle?handle=" + wantedHandles[x];
                var response = await BlueskyAPIHandleResolveConnection.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    //API get request
                    string stringResponse = await response.Content.ReadAsStringAsync();

                    //convert API call to array of objects. we only ever call 1 result from API so length will always be 1
                    HandleResolverAPI? result = JsonConvert.DeserializeObject<HandleResolverAPI>(stringResponse);

                    if (result == null)
                    {
                        //wantedDids.Add("");
                        Console.WriteLine($"{DateTime.Now}\tCouldn't find did for handle: {wantedHandles[x]}");
                    }
                    else
                    {
                        wantedDids.Add(result.did);
                        //Console.WriteLine($"Handle:{wantedHandles[x]}\tDid: {wantedDids.Last()}");
                    }
                }
                else
                {
                    //wantedDids.Add("");
                    Console.WriteLine($"{DateTime.Now}\tResponse code not successful for handle {wantedHandles[x]}\t Reason: {response.StatusCode}");
                }
            }

            string[] wantedDidsArray = wantedDids.ToArray();

            BlueskyAPIHandleResolveConnection.Dispose();    //no more need to keep API connection open

            atJetStream.ConnectAsync(wantedCollections: wantedCollections, wantedDids: wantedDidsArray).Wait();
            //atJetStream.ConnectAsync(wantedCollections: wantedCollections).Wait();


            var key = Console.ReadKey();
            await atJetStream.CloseAsync();
        }

        public async Task JetStream_ConnectionUpdated(SubscriptionConnectionStatusEventArgs args)
        {
            Console.WriteLine($"{DateTime.Now}\tJetStream Connection Updated\tState: {args.State}");
        }

        public async Task JetStream_RawMessageReceived(JetStreamRawMessageEventArgs args)
        {
            Console.WriteLine($"{DateTime.Now}\tJetStream Raw Message Received\tMessage JSON: {args.MessageJson}\n\n");
        }

        public async Task JetStream_RecordReceived(ATWebSocketRecord record)
        {
            /*
            Console.WriteLine($"JetStream Record Received\tKind: {record.Kind}\tDid:{record.Did}" +
            $"\tIdentity: {record.Identity}\tAccount: {record.Account}\tCommit: {record.Commit}" +
            $"\tTimeUs: {record.TimeUs}");
            */

            if (record.Commit != null)
                OnNewPost?.Invoke(this, record);
        }

    }

    public static class TaskExtensions
    {
        public static void FireAndForget(this Task task, Action<Exception> errorHandler = null)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            task.ContinueWith(t =>
            {
                if (errorHandler != null && t.IsFaulted)
                    errorHandler(t.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);

            // Avoiding warning about not awaiting the fire-and-forget task.
            // However, since the method is intended to fire and forget, we don't actually await it.
#pragma warning disable CS4014
            task.ConfigureAwait(false);
#pragma warning restore CS4014
        }
    }
}
