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
        // Dictionary to store routes and their corresponding handlers
        private static Dictionary<string, Func<Dictionary<string, string>, string>> GETroutes = new Dictionary<string, Func<Dictionary<string, string>, string>>();
        private static Dictionary<string, Func<Dictionary<string, string>, string>> POSTroutes = new Dictionary<string, Func<Dictionary<string, string>, string>>();
        // Dictionary to store file types based on URL
        Dictionary<string, string> GETFileTypes = new Dictionary<string, string>();
        Dictionary<string, string> POSTFileTypes = new Dictionary<string, string>();
        /// <summary>
        /// Function to listen on multiple ports and IPs
        /// </summary>
        /// <param name="ip">Array containing IPs that the server should listen on (FORMAT: http://IP:PORT)</param>
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
        /// Function to listen on a single address (useful for debugging, not for production)
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
        /// Handler of GET requests
        /// </summary>
        /// <param name="url">URL to handle</param>
        /// <param name="fileType">File type associated with the URL</param>
        /// <param name="handler">Handler function</param>
        public void get(string url, string fileType, Func<Dictionary<string, string>, string> handler)
        {
            GETroutes[url] = handler;
            GETFileTypes[url] = fileType;
        }

        /// <summary>
        /// Handler of POST requests
        /// </summary>
        /// <param name="url">URL to handle</param>
        /// <param name="fileType">File type associated with the URL</param>
        /// <param name="handler">Handler function</param>
        public void post(string url, string fileType, Func<Dictionary<string, string>, string> handler)
        {
            POSTroutes[url] = handler;
            POSTFileTypes[url] = fileType;
        }

        private void HandleRequest(HttpListenerContext context)
        {
            // Extract method and URL from the request
            string method = context.Request.HttpMethod;
            string url = context.Request.Url.LocalPath;

            string key = $"{method.ToUpper()}:{url}";

            if (method.ToUpper() == "GET")
            {
                // Parse query parameters
                var queryParams = context.Request.QueryString;

                // Get the handler and content type
                if (GETroutes.TryGetValue(url, out var handler))
                {
                    // Invoke the handler with the parsed query parameters
                    string response = handler.Invoke(QueryParamsToDictionary(queryParams));
                    SendResponse(context.Response, response, url, "GET");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("200 OK ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(url + "\n");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("404 NOT FOUND ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(url + "\n");
                    SendNotFoundResponse(context.Response, method, url);
                }
            }
            if (method.ToUpper() == "POST")
            {
                using (var reader = new StreamReader(context.Request.InputStream))
                {
                    string postData = reader.ReadToEnd();
                    var postParams = ParseQueryString(postData);
                    // Get the handler and content type
                    if (POSTroutes.TryGetValue(url, out var handler))
                    {
                        // Invoke the handler with the parsed query parameters
                        string response = handler.Invoke(postParams);
                        SendResponse(context.Response, response, url, "POST");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("200 OK ");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write(url + "\n");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("404 NOT FOUND ");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write(url + "\n");
                        SendNotFoundResponse(context.Response, method, url);
                    }
                }
            }
            // Close the response stream
            context.Response.Close();
        }

        private void SendResponse(HttpListenerResponse response, string content, string url, string METHODTYPE)
        {
            if(METHODTYPE == "POST")
            {
                if (POSTFileTypes.TryGetValue(url, out var contenttype))
                {
                    byte[] responseBytes = Encoding.UTF8.GetBytes($"{content}");
                    if (contenttype.Contains("application/octet-stream"))
                    {

                        responseBytes = Convert.FromBase64String(content);
                        response.AddHeader("Content-Disposition", $"inline; filename={Path.GetFileName(url)}");
                    }
                    response.ContentType = contenttype;
                    response.StatusCode = 200;
                    response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                }
                else
                {
                    byte[] responseBytes = Encoding.UTF8.GetBytes($"HTTP/1.1 500 Internal Server Error\r\nContent-Length: {content.Length}\r\n\r\n{content}");
                    response.StatusCode = 500;
                    response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                }
            } else
            {
                if (GETFileTypes.TryGetValue(url, out var contenttype))
                {
                    byte[] responseBytes = Encoding.UTF8.GetBytes($"{content}");
                    if (contenttype.Contains("application/octet-stream"))
                    {

                        responseBytes = Convert.FromBase64String(content);
                        response.AddHeader("Content-Disposition", $"inline; filename={Path.GetFileName(url)}");
                    }
                    response.ContentType = contenttype;
                    response.StatusCode = 200;
                    response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                }
                else
                {
                    byte[] responseBytes = Encoding.UTF8.GetBytes($"HTTP/1.1 500 Internal Server Error\r\nContent-Length: {content.Length}\r\n\r\n{content}");
                    response.StatusCode = 500;
                    response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                }
            }
        }

        private void SendNotFoundResponse(HttpListenerResponse response, string method, string url)
        {
            string content = $"404 Not Found: {method} {url}";
            byte[] responseBytes = Encoding.UTF8.GetBytes($"HTTP/1.1 404 Not Found\r\nContent-Length: {content.Length}\r\n\r\n{content}");
            response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
        }

        private Dictionary<string, string> QueryParamsToDictionary(System.Collections.Specialized.NameValueCollection queryParams)
        {
            Dictionary<string, string> queryParamsDict = new Dictionary<string, string>();
            foreach (string key in queryParams.AllKeys)
            {
                queryParamsDict[key] = queryParams[key];
            }
            return queryParamsDict;
        }
        private Dictionary<string, string> ParseQueryString(string queryString)
        {
            Dictionary<string, string> queryParamsDict = new Dictionary<string, string>();

            var keyValuePairs = queryString.Split('&');
            foreach (var pair in keyValuePairs)
            {
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    queryParamsDict[keyValue[0]] = WebUtility.UrlDecode(keyValue[1]);
                }
            }

            return queryParamsDict;
        }
    }
}
