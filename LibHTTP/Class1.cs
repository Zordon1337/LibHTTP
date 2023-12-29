using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace LibHTTP
{
    public class HTTP
    {
        private readonly Dictionary<string, Func<string>> routes = new Dictionary<string, Func<string>>();
        private static bool bServerStarted = false;

        /// <summary>
        /// Function to listen, but on multiple ports and IPs
        /// </summary>
        /// <param name="ip">array which contains IPs that the server should listen on, FORMAT: http://IP:PORT</param>
        public void ListenMA(string[] ip)
        {
            using (HttpListener listener = new HttpListener())
            {
                foreach (var addr in ip)
                {
                    listener.Prefixes.Add(addr);
                }
                listener.Start();
                Console.WriteLine($"Server listening at {string.Join(", ", listener.Prefixes)}");

                ThreadPool.QueueUserWorkItem((state) =>
                {
                    while (listener.IsListening)
                    {
                        try
                        {
                            // Wait for a request and get the context
                            HttpListenerContext context = listener.GetContext();

                            // Process the request in a separate thread
                            ThreadPool.QueueUserWorkItem((ctx) => HandleRequest(context));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                });

                // Keep the console application running
                Console.ReadLine();
            }
        }
        /// <summary>
        /// Function to listen on single address(useful for debugging)
        /// </summary>
        /// <param name="ip">FORMAT: http://IP:PORT</param>
        public void Listen(string ip)
        {
            using (HttpListener listener = new HttpListener())
            {
                listener.Prefixes.Add(ip);
                listener.Start();
                Console.WriteLine($"Server listening at {string.Join(", ", listener.Prefixes)}");

                ThreadPool.QueueUserWorkItem((state) =>
                {
                    while (listener.IsListening)
                    {
                        try
                        {
                            // Wait for a request and get the context
                            HttpListenerContext context = listener.GetContext();

                            // Process the request in a separate thread
                            ThreadPool.QueueUserWorkItem((ctx) => HandleRequest(context));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                });

                // Keep the console application running
                Console.ReadLine();
            }
        }
        /// <summary>
        /// Handler of GET, USAGE: httpServer.Get("/index", () => { "Hi" });
        /// </summary>
        /// <param name="url"></param>
        /// <param name="handler"></param>
        public void Get(string url, Func<string> handler)
        {
            string key = $"GET:{url}";
            routes[key] = handler;
        }

        private void HandleRequest(HttpListenerContext context)
        {
            // Extract method and URL from the request
            string method = context.Request.HttpMethod;
            string url = context.Request.Url.LocalPath;

            string key = $"{method.ToUpper()}:{url}";
            if (routes.TryGetValue(key, out var handler))
            {
                string response = handler.Invoke();
                SendResponse(context.Response, response);
            }
            else
            {
                SendNotFoundResponse(context.Response, method, url);
            }

            // Close the response stream
            context.Response.Close();
        }

        private void SendResponse(HttpListenerResponse response, string content)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {content.Length}\r\n\r\n{content}");
            response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
        }

        private void SendNotFoundResponse(HttpListenerResponse response, string method, string url)
        {
            string content = $"404 Not Found: {method} {url}";
            byte[] responseBytes = Encoding.UTF8.GetBytes($"HTTP/1.1 404 Not Found\r\nContent-Length: {content.Length}\r\n\r\n{content}");
            response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
        }
    }
}
