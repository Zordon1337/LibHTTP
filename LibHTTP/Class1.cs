using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading;

namespace LibHTTP
{
    public class HTTP
    {
        private static Dictionary<string, Func<string>> routes = new Dictionary<string, Func<string>>();
        Dictionary<string, string> FileTypes = new Dictionary<string, string>();

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
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Server listening at {string.Join(", ", listener.Prefixes)}");
                Console.ForegroundColor = ConsoleColor.White;
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
                // avoid server exiting
                Console.ReadLine();

            }
        }
        /// <summary>
        /// Function to listen on single address(useful for debugging BUT not for production(this framework shouldn't be even used for production))
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
                // avoid server exiting
                Console.ReadLine();
            }
        }
        /// <summary>
        /// Handler of GET, USAGE: httpServer.Get("/index", () => { return "Hi" });
        /// </summary>
        /// <param name="url"></param>
        /// <param name="handler"></param>
        public void Get(string url, Func<string> handler,string FileType)
        {
            //Console.WriteLine($"Adding route: {url}");
            routes[url] = handler;
            FileTypes[url] = FileType;
        }


        private void HandleRequest(HttpListenerContext context)
        {
            // Extract method and URL from the request
            string method = context.Request.HttpMethod;
            string url = context.Request.Url.LocalPath;

            string key = $"{method.ToUpper()}:{url}";
            
            
            if (routes.TryGetValue(url, out var handler))
            {
                
                string response = handler.Invoke();
                SendResponse(context.Response, response, url);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\n200 OK ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(url);
                
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("\n404 NOT FOUND ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(url);
                SendNotFoundResponse(context.Response, method, url);
            }


            // Close the response stream
            context.Response.Close();
        }

        private void SendResponse(HttpListenerResponse response, string content, string url)
        {
            if(FileTypes.TryGetValue(url, out var contenttype))
            {
                byte[] responseBytes = Encoding.UTF8.GetBytes($"{content}");
                response.ContentType = contenttype;
                response.StatusCode = 200;
                response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            } else
            {
                byte[] responseBytes = Encoding.UTF8.GetBytes($"HTTP/1.1 500 Internal Server Error\r\nContent-Length: {content.Length}\r\n\r\n{content}");
                response.StatusCode = 500;
                response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            }
            
            
        }
        
        

        private void SendNotFoundResponse(HttpListenerResponse response, string method, string url)
        {
            string content = $"404 Not Found: {method} {url}";
            byte[] responseBytes = Encoding.UTF8.GetBytes($"HTTP/1.1 404 Not Found\r\nContent-Length: {content.Length}\r\n\r\n{content}");
            
            response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
        }
    }
}
