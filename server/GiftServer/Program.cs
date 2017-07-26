﻿using System;
using System.Net;
using System.IO;
using System.Web;
using System.Collections.Specialized;
using GiftServer.Data;
namespace GiftServer
{
    public class Program
    {
        /// <summary>
        /// Main method and entry point - starts the web server on the specified host/port.
        /// </summary>
        /// <param name="args">Reserved for future use</param>
        public static void Main(string[] args)
        {
            Server.WebServer server = new Server.WebServer("http://localhost:60001/", Dispatch);
            server.Run();
            Console.WriteLine("Server is Active...\nType quit or q to quit");
            string input = null;
            while ((input = Console.ReadLine()) != null)
            {
                if (string.Equals(input.ToLower(), "quit") || string.Equals(input.ToLower(), "q"))
                {
                    server.Stop();
                    return;
                }
            }
        }
        /// <summary>
        /// Dispatch will, given a request, return the webpage that will be shown to the user.
        /// </summary>
        /// <param name="request">The incoming HTML request, in it's entirety</param>
        /// <returns>The html to be sent back to the user. Additionally, it will also alter the response, if necessary</returns>
        public static string Dispatch(HttpListenerContext rtx)
        {
            // Check if user is logged in (via cookies?)
            HttpListenerRequest request = rtx.Request;
            HttpListenerResponse response = rtx.Response;
            bool isLoggedIn = false;
            if (request.HasEntityBody)
            {
                string input;
                // Read input, then dispatch accordingly
                using (StreamReader reader = new StreamReader(request.InputStream))
                {
                    input = reader.ReadToEnd();
                    NameValueCollection dict = HttpUtility.ParseQueryString(input);
                    if (dict["submit"] != null)
                    {
                        // Dispatch to correct logic:
                        switch (dict["submit"])
                        {
                            case "Signup":
                                User newUser = new User(dict["firstName"], dict["lastName"], dict["email"], dict["password"]);
                                newUser.Create();
                                break;
                            case "Login":
                                User returnUser = new User(dict["email"], dict["password"]);
                                break;
                            default:
                                break;
                        }
                    }
                    foreach (string key in dict.AllKeys)
                    {
                        Console.WriteLine(key + " - " + dict[key]);
                    }
                }
                return "<html><body><form method=\"POST\"><input name=\"theMail\" type=\"email\"/><button type=\"submit\" value=\"submit\">Hello</button></form></body></html>";
            }
            else if (isLoggedIn)
            {
                // If logged in (but no request), just send back home page:
                return "<html><body><form method=\"POST\"><input name=\"theMail\" type=\"email\"/><button type=\"submit\" value=\"submit\">Hello</button></form></body></html>";
            }
            else
            {
                // If not logged in, send the login page!
                return GiftServer.Properties.Resources.login;
            }
        }
    }
}
