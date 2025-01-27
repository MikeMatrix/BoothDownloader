﻿using System.Net;
using System.Text.RegularExpressions;
using BoothDownloader.config;
using BoothDownloader.log;
using Microsoft.Extensions.Logging;

namespace BoothDownloader.web;

public class BoothClient
{
    private const string UrlAccountSettings = "https://accounts.booth.pm/settings";

    private static readonly Regex
        GuidRegex = new(@"[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(png|jpg|gif)");

    private static readonly Regex DownloadNameRegex = new(@".*\/(.*)\?");
    private Config Config { get; }

    public BoothClient(Config config)
    {
        Config = config;
    }

    private HttpClientHandler? _clientHandler;
    private HttpClientHandler GetClientHandler()
    {
        if (_clientHandler == null)
        {
            var baseUri = new Uri(".booth.pm");
            var cookieContainer = new CookieContainer();
            cookieContainer.Add(baseUri, new Cookie("adult", "t"));
            cookieContainer.Add(baseUri, new Cookie("_plaza_session_nktz7u", Config.Cookie));
            _clientHandler = new HttpClientHandler { CookieContainer = cookieContainer };
        }

        return _clientHandler;
    }
    public HttpClient GetClient()
    {
        return new HttpClient(GetClientHandler());
    }
    
    public ResponseUriWebClient MakeWebClient()
    {
        var client = new ResponseUriWebClient();
        client.Headers.Add(HttpRequestHeader.Cookie, $"adult=t; _plaza_session_nktz7u={Config.Cookie}");

        return client;
    }

    public bool IsCookieValid()
    {
        var client = MakeWebClient();
        client.DownloadString(UrlAccountSettings);

        return client.ResponseUri!.ToString() == UrlAccountSettings;
    }

    public Task StartDownloadImageTask(string url, DirectoryInfo targetDirectory)
    {
        return Task.Factory.StartNew(() =>
        {
            var logger = Log.Factory.CreateLogger("DownloadImageTask");
            using (logger.BeginScope(url))
            {
                using var webClient = MakeWebClient();
                logger.LogInformation("Starting Image Download [{0}]", Environment.CurrentManagedThreadId);
                var result = webClient.DownloadData(url);
                var name = GuidRegex.Match(url).ToString().Split('/').Last();
                File.WriteAllBytesAsync(targetDirectory + "/" + name, result);
                logger.LogInformation("Finished Image Download [{0}]", Environment.CurrentManagedThreadId);
            }
        });
    }

    public Task StartDownloadBinaryTask(string url, DirectoryInfo targetDirectory)
    {
        return Task.Factory.StartNew(() =>
        {
            var logger = Log.Factory.CreateLogger("DownloadTask");
            using (logger.BeginScope(url))
            {
                using var webClient = MakeWebClient();
                logger.LogInformation("Starting Download [{0}]", Environment.CurrentManagedThreadId);
                var result = webClient.DownloadData(url);
                var filename = DownloadNameRegex.Match(webClient.ResponseUri!.ToString()).Groups[1].Value;
                File.WriteAllBytesAsync(targetDirectory + "/" + filename, result);
                logger.LogInformation("Finished Download [{0}]", Environment.CurrentManagedThreadId);
            }
        });
    }
}