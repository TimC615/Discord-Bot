using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Core.Exceptions;
using Tweetinvi.Models;
using Tweetinvi.Parameters;
using Tweetinvi.Parameters.V2;
using static FishyFlip.Constants;
using static System.Runtime.InteropServices.JavaScript.JSType;
using TwitterSharp;
using TwitterSharp.Client;
using TwitterSharp.Request;
using System.Linq.Expressions;
using TwitterSharp.Rule;
using TwitterSharp.Request.AdvancedSearch;
using TwitterSharp.Request.Option;
using System.Timers;
using Microsoft.Extensions.Configuration;

namespace Majin_Discord_Bot
{
    public class TwitterUserSearchInfo
    {
        public string Username { get; set; }
        public string UserId { get; set; }
    }

    public class TwitterNewPostResponse
    {
        public string Username { get; set; }
        public string PostId { get; set; }
    }

    internal class Twitter
    {
        private IConfiguration config;

        public event EventHandler<TwitterNewPostResponse> OnNewPost;


        Tweetinvi.TwitterClient twitter;
        TwitterSharp.Client.TwitterClient twitterSharp;

        List<TwitterUserSearchInfo> userIdList = new List<TwitterUserSearchInfo>
            {
                new TwitterUserSearchInfo{ Username = "CBCNews", UserId = "6433472"}
            };

        static int timerInterval = 12 * (60 * 60 * 1000);  //converts 12 hours to milliseconds
        static private System.Timers.Timer checkNewTweetsTimer = new System.Timers.Timer(timerInterval);

        public async void ConnectToTwitter(IConfiguration config)
        {
            this.config = config;

            twitterSharp = new TwitterSharp.Client.TwitterClient(config.GetValue<string>("Twitter:BearerToken"));

            //Console.WriteLine($"appsettings.json MostRecentTweetId: {config.GetValue<string>("Twitter:MostRecentTweetId")}");


            checkNewTweetsTimer.Elapsed += OnTweetTimerElapsed;
            checkNewTweetsTimer.AutoReset = true;

            Console.WriteLine($"First CheckNewTweets Timer went off at {DateTime.Now}");
            GetNewTweets();   //put call to GetNewTweets() so bot doesn't need to wait 12 hours from startup to do a first check

            checkNewTweetsTimer.Start();
        }

        private void OnTweetTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            
            //Console.WriteLine($"CheckNewTweets Timer went off at {e.SignalTime}");
            GetNewTweets();
        }

        private async void GetNewTweets()
        {
            string filePath = config.GetValue<string>("Twitter:TweetIdFilePath");
            string fileName;
            string[] fileInput;
            string mostRecentTweetId;

            //handles if provided file path is accessable or not
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"There was an issue with the selected file. Please make sure file path is set to a readable text file.");
                return;
            }
            else
            {
                try
                {
                    fileName = System.IO.Path.GetFileName(filePath);

                    fileInput = File.ReadAllLines(filePath);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Read File Error: {e.Message}");
                    return;
                }

                //handles when user tries to use a file with nothing inside it
                if (fileInput.Length == 0)
                {
                    Console.WriteLine($"File contains no tweet id");
                    return;
                }
                else
                {
                    mostRecentTweetId = fileInput[0];

                    Console.WriteLine($"TEST: {mostRecentTweetId}");
                }
            }

            //string mostRecentTweetIdV2 = "1851644787889189189"; //https://x.com/MajinOrca/status/1851644787889189189

            try
            {
                var tweetsResponse = await twitterSharp.GetTweetsFromUserIdAsync(userIdList[0].UserId, new TweetSearchOptions
                {
                    SinceId = mostRecentTweetId,
                    Limit = 20,
                    TweetOptions = new[] { TweetOption.Source, TweetOption.Public_Metrics }
                });

                //iterate through tweets in reverse to handle tweets from oldest to newest
                for (int x = tweetsResponse.Length - 1; x >= 0; x--)
                {
                    var tweet = tweetsResponse[x];

                    Console.WriteLine($"Tweet {x + 1}: {tweet.Text}");
                    OnNewPost?.Invoke(this, new TwitterNewPostResponse { Username = userIdList[0].Username, PostId = tweet.Id });

                    if (x == tweetsResponse.Length - 1)
                    {
                        WriteMostRecentTweetIdToFile(filePath, fileName, tweet.Id);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{DateTime.Now}\tGetNewTweets() Error: {e.Message}");
            }
        }

        private void WriteMostRecentTweetIdToFile(string filePath, string fileName, string mostRecentTweetId)
        {
            try
            {
                File.WriteAllText(filePath, mostRecentTweetId);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Write to {fileName} error: {e.Message}");
            }
        }
    }
}
