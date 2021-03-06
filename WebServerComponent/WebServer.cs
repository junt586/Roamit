﻿using QuickShare.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;

namespace QuickShare.UWP
{
    public class WebServer : IWebServer
    {
        HttpListener listener;
        Dictionary<string, object> Urls = new Dictionary<string, object>();

        string ip;
        int port;

        Guid guid = Guid.NewGuid();

        public void StartWebServer(string _ip, int _port)
        {
            ip = _ip;
            port = _port;

            listener = new HttpListener(IPAddress.Parse(_ip), _port);
            listener.Request += Listener_Request;
            listener.Start();

            AddRootPage();
        }

        public string DefaultRootPage()
        {
            return "<html><head><title>Roamit</title></head><body><h3>Hello from Roamit :)</h3></body></html>";
        }

        private void AddRootPage()
        {
            AddResponseUrl("/", DefaultRootPage());
        }

        public void AddResponseUrl(string url, string response) { AddResponseUrlInternal(url, response); }
        public void AddResponseUrl(string url, byte[] response) { AddResponseUrlInternal(url, response); }
        public void AddResponseUrl(string url, Func<IWebServer, RequestDetails, string> response) { AddResponseUrlInternal(url, response); }
        public void AddResponseUrl(string url, Func<IWebServer, RequestDetails, byte[]> response) { AddResponseUrlInternal(url, response); }
        public void AddResponseUrl(string url, Func<IWebServer, RequestDetails, Task<string>> response) { AddResponseUrlInternal(url, response); }
        public void AddResponseUrl(string url, Func<IWebServer, RequestDetails, Task<byte[]>> response) { AddResponseUrlInternal(url, response); }

        private void AddResponseUrlInternal(string url, object response)
        {
            Debug.WriteLine($"Added url {url} to listener {guid}.");

            if (Urls.ContainsKey(url))
                Urls.Remove(url);

            Urls.Add(url, response);
        }

        public void ClearResponseUrls()
        {
            Urls.Clear();
            AddRootPage();
        }

        public void RemoveResponseUrl(string url)
        {
            Urls.Remove(url);
        }

        private async void Listener_Request(object sender, HttpListenerRequestEventArgs e)
        {
            if (Urls.ContainsKey(e.Request.RequestUri.AbsolutePath))
            {
                RequestDetails rd = new RequestDetails
                {
                    Headers = new Dictionary<string, string>(e.Request.Headers.Select(x => new KeyValuePair<string, string>(x.Key, x.Value.ToString())).ToDictionary(x => x.Key, x => x.Value)),
                    Host = e.Request.RequestUri.Host,
                    HttpMethod = e.Request.Method,
                    InputStream = e.Request.InputStream,
                    ProtocolVersion = e.Request.Version,
                    RemoteEndpointAddress = e.Request.RemoteEndpoint.Address.ToString(),
                    Url = e.Request.RequestUri,
                };

                var value = Urls[e.Request.RequestUri.AbsolutePath];
                if (value is string)
                {
                    await e.Response.WriteContentAsync(value as string);
                }
                else if (value is byte[])
                {
                    byte[] b = (byte[])value;
                    await e.Response.OutputStream.WriteAsync(b, 0, b.Count());
                }
                else if (value is Func<IWebServer, RequestDetails, string>)
                {
                    var output = ((Func<IWebServer, RequestDetails, string>)value).Invoke(this, rd);
                    await e.Response.WriteContentAsync(output);
                }
                else if (value is Func<IWebServer, RequestDetails, byte[]>)
                {
                    var output = ((Func<IWebServer, RequestDetails, byte[]>)value).Invoke(this, rd);
                    await e.Response.OutputStream.WriteAsync(output, 0, output.Count());
                }
                else if (value is Func<IWebServer, RequestDetails, Task<string>>)
                {
                    var output = await ((Func<IWebServer, RequestDetails, Task<string>>)value).Invoke(this, rd);
                    await e.Response.WriteContentAsync(output);
                }
                else if (value is Func<IWebServer, RequestDetails, Task<byte[]>>)
                {
                    var output = await ((Func<IWebServer, RequestDetails, Task<byte[]>>)value).Invoke(this, rd);
                    await e.Response.OutputStream.WriteAsync(output, 0, output.Count());
                }
                else
                {
                    await e.Response.WriteContentAsync("<html><body>Invalid url handler.</body></html>");
                }
            }
            else
            {
                Debug.WriteLine($"Listener {guid} received an invalid request: {e.Request.RequestUri.AbsolutePath}");
                await e.Response.WriteContentAsync("<html><body>Invalid Request.</body></html>");
            }

            e.Response.Close();
        }

        public void StopListener()
        {
            Debug.WriteLine($"Listener of WebServer {guid} is closing...");
            listener.Close();
            listener = null;
        }

        public void Dispose()
        {
            Debug.WriteLine($"WebServer {guid} is going down...");
            listener?.Close();
        }
    }
}
