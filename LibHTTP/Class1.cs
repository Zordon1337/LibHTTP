﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace LibHTTP
{
    public class HTTP
    {
        // Dictionary to store routes and their corresponding handlers
        private static Dictionary<string, Func<Dictionary<string, string>, string>> GETroutes = new Dictionary<string, Func<Dictionary<string, string>, string>>();
        private static Dictionary<string, Func<Dictionary<string, string>, string>> POSTroutes = new Dictionary<string, Func<Dictionary<string, string>, string>>();
        private Dictionary<string, string> currentParams;
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
            string method = context.Request.HttpMethod;
            string url = context.Request.Url.LocalPath;

            string key = $"{method.ToUpper()}:{url}";

            if (method.ToUpper() == "GET")
            {
                currentParams = ParseRequestParameters(context.Request);

                if (GETroutes.TryGetValue(url, out var handler))
                {
                    string response = handler.Invoke(currentParams);
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
            else if (method.ToUpper() == "POST")
            {
                if (context.Request.HasEntityBody)
                {
                    currentParams = HandleRequestData(context.Request);
                    HandleFileUpload(context, (fileData) =>
                    {
                        currentParams["file"] = fileData;
                        HandlePostRequest(context, url, method);
                    });
                }
                else
                {
                    HandlePostRequest(context, url, method);
                }
            }

            context.Response.Close();
        }
        private void HandlePostRequest(HttpListenerContext context, string url, string method)
        {
            if (POSTroutes.TryGetValue(url, out var handler))
            {
                string response = handler.Invoke(currentParams);
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

        private void HandleFileUpload(HttpListenerContext context, Action<string> handleFileCallback)
        {
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                string fileData = reader.ReadToEnd();
                handleFileCallback.Invoke(fileData);
            }
        }

        private Dictionary<string, string> ParseRequestParameters(HttpListenerRequest request)
        {
            Dictionary<string, string> paramsDict = new Dictionary<string, string>();
            var queryParams = request.QueryString;
            foreach (string key in queryParams.AllKeys)
            {
                paramsDict[key] = queryParams[key];
            }

            return paramsDict;
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
        private Dictionary<string, string> HandleRequestData(HttpListenerRequest request)
        {
            if (request.HasEntityBody)
            {
                using (var reader = new StreamReader(request.InputStream))
                {
                    string requestData = reader.ReadToEnd();
                    return ParseQueryString(requestData);
                }
            }

            return new Dictionary<string, string>();
        }

        public Dictionary<string, string> HandleFileUpload(HttpListenerContext context)
        {
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                string fileData = reader.ReadToEnd();

                // Add the file data to the currentParams dictionary
                currentParams["file"] = fileData;

                // Call the registered callback to handle the file data
                return currentParams;
            }

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
