﻿using GiftServer.Data;
using GiftServer.Exceptions;
using GiftServer.Html;
using GiftServer.Properties;
using GiftServer.Security;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Web;

namespace GiftServer
{
    namespace Server
    {
        public class Controller
        {

            public static readonly List<Connection> Connections = new List<Connection>();
            public static readonly List<Warning> Warnings = new List<Warning>();
            public CultureInfo Culture
            {
                get
                {
                    return culture;
                }
            }
            private readonly static Object key = new Object();
            private CultureInfo culture;
            private User _user;
            private HttpListenerContext _ctx;
            private HttpListenerRequest _request;
            private HttpListenerResponse _response;
            private NameValueCollection _dict;

            public readonly LoginManager LoginManager;
            public readonly NavigationManager NavigationManager;
            public readonly GroupManager GroupManager;
            public readonly DashboardManager DashboardManager;
            public readonly ProfileManager ProfileManager;
            public readonly ListManager ListManager;
            public readonly ResetManager ResetManager;

            public Controller(HttpListenerContext ctx)
            {
                _ctx = ctx;
                _request = ctx.Request;
                _response = ctx.Response;
                GetUser();
                LoginManager = new LoginManager(this);
                NavigationManager = new NavigationManager(this);
                GroupManager = new GroupManager(this);
                DashboardManager = new DashboardManager(this);
                ProfileManager = new ProfileManager(this);
                ListManager = new ListManager(this);
                ResetManager = new ResetManager(this);
            }
            /// <summary>
            /// Dispatch will, given a request, return the webpage that will be shown to the user.
            /// </summary>
            /// <remarks>Dispatch is used to communicate with the server</remarks>
            /// <returns>The html to be sent back to the user. Additionally, it will also alter the response, if necessary</returns>
            public string Dispatch()
            {
                try
                {
                    if (!_request.Url.OriginalString.Contains("https://"))
                    {
                        _response.Redirect(Constants.URL);
                        return null;
                    }
                    string path = ParsePath();
                    if (_request.ContentType != null && _request.ContentType.Contains("multipart/form-data"))
                    {
                        MultipartParser parser = new MultipartParser(_request.InputStream, "image");
                        if (parser.Success)
                        {
                            // Image file will be saved in resources/images/users/User[UID].jpg
                            // Figure out which page user was on, engage.
                            if (_request.QueryString["dest"] != null)
                            {
                                switch (_request.QueryString["dest"])
                                {
                                    case "profile":
                                        {
                                            // Save user image:
                                            _user.SaveImage(parser);
                                            break;
                                        }
                                    case "myList":
                                        {
                                            Gift gift = new Gift(Convert.ToUInt64(parser.Parameters["itemid"]));
                                            gift.SaveImage(parser);
                                            break;
                                        }
                                }
                                return ParseQuery();
                            }
                            else
                            {
                                // Just return dashboard
                                return DashboardManager.Dashboard(_user);
                            }
                        }
                    }
                    if (_request.HasEntityBody)
                    {
                        string input;
                        // Read input, then dispatch accordingly
                        using (StreamReader reader = new StreamReader(_request.InputStream))
                        {
                            input = reader.ReadToEnd();
                            _dict = HttpUtility.ParseQueryString(input);
                            if (_dict["submit"] != null)
                            {
                                // Dispatch to correct logic:
                                switch (_dict["submit"])
                                {
                                    case "Logout":
                                        Logout();
                                        return LoginManager.Login();
                                    case "Login":
                                        return Login(_dict["email"], _dict["password"]);
                                    case "PasswordResetRequest":
                                        // POST data will have user email. Send recovery email.
                                        PasswordReset.SendRecoveryEmail(_dict["email"], ResetManager);
                                        return ResetManager.ResetPasswordSent();
                                    case "PasswordReset":
                                        // Reset password and direct to login page
                                        // POST data will have userID in userID input. Reset the password and let the user know.
                                        _user = new User(Convert.ToUInt64(_dict["userID"]));
                                        string password = _dict["password"];
                                        _user.UpdatePassword(password, ResetManager);
                                        return ResetManager.SuccessResetPassword();
                                    case "Change":
                                        ulong changeId = Convert.ToUInt64(_dict["itemId"]);
                                        switch (_dict["type"])
                                        {
                                            case "User":
                                                return Update();
                                            case "Preferences":
                                                return Update(_user.Preferences);
                                            case "Event":
                                                return Update(new EventUser(changeId));
                                            case "Group":
                                                return Update(new Group(changeId));
                                            case "Gift":
                                                return Update(new Gift(changeId));
                                            default:
                                                return "";
                                        }
                                    case "Create":
                                        switch (_dict["type"])
                                        {
                                            case "User":
                                                try
                                                {
                                                    _user = new User(new MailAddress(_dict["email"]), new Password(_dict["password"]))
                                                    {
                                                        UserName = _dict["userName"]
                                                    };
                                                    _user.Create();
                                                    return LoginManager.SuccessSignup();
                                                }
                                                catch (Exception e)
                                                {
                                                    return LoginManager.FailLogin(e);
                                                }
                                            case "Group":
                                                try
                                                {
                                                    Group group = new Group(_user, _dict["name"]);
                                                    group.Create();
                                                    return group.GroupId.ToString();
                                                }
                                                catch (Exception)
                                                {
                                                    // What do we do?
                                                    return "0";
                                                }
                                            case "Event":

                                            case "Gift":
                                                try
                                                {
                                                    Gift gift = new Gift(_dict["name"])
                                                    {
                                                        Description = _dict["description"],
                                                        Cost = Convert.ToDouble(_dict["cost"] == null || _dict["cost"].Length == 0 ? "0.00" : _dict["cost"]),
                                                        Quantity = Convert.ToUInt32(_dict["quantity"] == null || _dict["quantity"].Length == 0 ? "1" : _dict["quantity"]),
                                                        Rating = Convert.ToDouble(_dict["rating"] == null || _dict["rating"].Length == 0 ? "0.0" : _dict["rating"]),
                                                        Size = _dict["size"],
                                                        Url = _dict["url"],
                                                        Stores = _dict["stores"],
                                                        User = _user,
                                                        ColorText = _dict["colorText"],
                                                        Category = new Category(_dict["category"])
                                                        // Color = _dict["color"],
                                                    };
                                                    gift.Create();
                                                    return "200";
                                                }
                                                catch (Exception e)
                                                {
                                                    Warnings.Add(new ExecutionErrorWarning(e));
                                                    return "0";
                                                }
                                            default:
                                                return "";
                                        }
                                    case "Fetch":
                                        ulong fetchId = Convert.ToUInt64(_dict["itemId"]);
                                        IFetchable item = null;
                                        switch (_dict["type"])
                                        {
                                            case "Gift":
                                                item = new Gift(fetchId);
                                                break;
                                            case "User":
                                                item = new User(fetchId);
                                                break;
                                            case "Event":
                                                item = new EventUser(fetchId);
                                                break;
                                            case "Group":
                                                item = new Group(fetchId);
                                                break;
                                            default:
                                                _response.StatusCode = 404;
                                                return "Specified information not found";
                                        }
                                        return item.Fetch().OuterXml;
                                    default:
                                        return LoginManager.Login();
                                    case "Query":
                                        switch (_dict["type"])
                                        {
                                            case "Email":
                                                return Fetch(_dict["email"]);
                                            default:
                                                _response.StatusCode = 404;
                                                return "Corrupted Query";
                                        }
                                }
                            }
                            else
                            {
                                return LoginManager.Login();
                            }
                        }
                    }
                    else if (path.Length != 0)
                    {
                        return ServeResource(path);
                    }
                    else if (_user == null)
                    {
                        // Send login page EXCEPT if requesting password reset:
                        if (_request.QueryString["ResetToken"] != null)
                        {
                            try
                            {
                                return ResetManager.CreateReset(PasswordReset.GetUser(_request.QueryString["ResetToken"]));
                            }
                            catch (PasswordResetTimeoutException)
                            {
                                return ResetManager.ResetFailed();
                            }

                        }
                        else
                        {
                            return LoginManager.Login();
                        }
                    }
                    else if (_request.QueryString["dest"] != null)
                    {
                        return ParseQuery();
                    }
                    else
                    {
                        // If logged in (but no request), just send back home page:
                        return DashboardManager.Dashboard(_user);
                    }
                // catch exceptions and return something meaningful
                }
                catch (Exception e)
                {
                    _response.StatusCode = 404;
                    Warnings.Add(new ExecutionErrorWarning(e));
                    return "";
                }
            }

            private string ParseQuery()
            {
                string query = _request.QueryString["dest"];
                switch (query)
                {
                    case "dashboard":
                        return DashboardManager.Dashboard(_user);
                    case "profile":
                        return ProfileManager.ProfilePage(_user);
                    case "myList":
                        return ListManager.GiftList(_user);
                    case "list":
                        return ListManager.PublicList(new User(_request.QueryString["user"]));
                    case "user":
                        return ProfileManager.PublicProfile(new User(_request.QueryString["user"]));
                    default:
                        return DashboardManager.Dashboard(_user);
                }
            }

            private string ServeResource(string serverPath)
            {
                string message = null;
                string path = GeneratePath(serverPath);
                // Check existence:
                if (File.Exists(path))
                {
                    // File exists: Check if filename even needs authentication:
                    if (Path.GetFileName(Path.GetDirectoryName(path)).Equals("users"))
                    {
                        if (Path.GetFileNameWithoutExtension(path).Equals("default"))
                        {
                            // Serve up immediately
                            Write(path);
                            Warnings.Add(new PublicResourceWarning(path));
                        }
                        else if (_user != null)
                        {
                            if (Path.GetFileNameWithoutExtension(path).Equals("User" + _user.UserId))
                            {
                                // This user - write path
                                Write(path);
                            }
                            else
                            {
                                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                                {
                                    con.Open();
                                    using (MySqlCommand cmd = new MySqlCommand())
                                    {
                                        // See if our user (_user) and requested user (from string) have common groups
                                        // Subquery selects Groups tied to _user; 
                                        cmd.Connection = con;
                                        cmd.CommandText = "SELECT users.UserID "
                                                        + "FROM users "
                                                        + "INNER JOIN groups_users ON groups_users.UserID = users.UserID "
                                                        + "WHERE groups_users.UserID = @otherID "
                                                        + "AND groups_users.GroupID IN "
                                                        + "( "
                                                            + "SELECT GroupID FROM groups_users WHERE groups_users.UserID = @meID "
                                                        + ");";
                                        cmd.Parameters.AddWithValue("@meID", _user.UserId);
                                        cmd.Parameters.AddWithValue("@otherID", Path.GetFileNameWithoutExtension(path).Substring(4));
                                        cmd.Prepare();
                                        using (MySqlDataReader reader = cmd.ExecuteReader())
                                        {
                                            if (reader.Read())
                                            {
                                                // connected
                                                Write(path);
                                            }
                                            else
                                            {
                                                _response.StatusCode = 403;
                                                message = "Forbidden - You are not in any common groups with this user.";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (Path.GetFileName(Path.GetDirectoryName(path)).Equals("gifts"))
                    {
                        // If default image is desired, serve up immediately:
                        if (Path.GetFileNameWithoutExtension(path).Equals("default"))
                        {
                            Write(path);
                            Warnings.Add(new PublicResourceWarning(path));
                        }
                        else if (_user != null)
                        {
                            // If GiftID and UserID match, we will be able to read; otherwise, no
                            // Get GID:
                            ulong gid = Convert.ToUInt64(Path.GetFileNameWithoutExtension(path).Substring(4));
                            if (_user.Gifts.Exists(new Predicate<Gift>((Gift g) => g.GiftId == gid)))
                            {
                                // Found in our own gifts; write
                                Write(path);
                            }
                            else
                            {
                                // See if tied to gift through groups_gifts
                                // Subquery selects groups tied to user:
                                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                                {
                                    con.Open();
                                    using (MySqlCommand cmd = new MySqlCommand())
                                    {
                                        cmd.Connection = con;
                                        cmd.CommandText = "SELECT GiftID "
                                                        + "FROM groups_gifts "
                                                        + "WHERE groups_gifts.GiftID = @gid "
                                                        + "AND groups_gifts.GroupID IN "
                                                        + "( "
                                                            + "SELECT GroupID FROM groups_users WHERE groups_users.UserID = @uid "
                                                        + ");";
                                        cmd.Parameters.AddWithValue("@gid", gid);
                                        cmd.Parameters.AddWithValue("@uid", _user.UserId);
                                        cmd.Prepare();
                                        using (MySqlDataReader reader = cmd.ExecuteReader())
                                        {
                                            if (reader.Read())
                                            {
                                                // Tied; write
                                                Write(path);
                                            }
                                            else
                                            {
                                                _response.StatusCode = 403;
                                                message = "Forbidden - this gift is not currently shared with you.";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Not accessing images or gifts, so OK to just send info:
                        Write(path);
                        Warnings.Add(new PublicResourceWarning(path));
                    }
                }
                else if (Path.GetFileNameWithoutExtension(path).Equals("favicon"))
                {
                    Write(Constants.favicon);
                }
                else
                {
                    _response.StatusCode = 404;
                    message = "File Not Found: Unknown resource " + serverPath + ".";
                }
                return message;
            }

            private void Write(string path)
            {
                switch (Path.GetExtension(path))
                {
                    case "bm":
                    case "bmp":
                        _response.ContentType = "image/bmp";
                        break;
                    case "css":
                        _response.ContentType = "text/css";
                        break;
                    case "gif":
                        _response.ContentType = "image/gif";
                        break;
                    case "jpe":
                    case "jpeg":
                    case "jpg":
                        _response.ContentType = "image/jpeg";
                        break;
                    case "js":
                        _response.ContentType = "text/javascript";
                        break;
                    case "png":
                        _response.ContentType = "image/png";
                        break;
                    default:
                        break;
                }
                Write(File.ReadAllBytes(path));
            }
            private void Write(System.Drawing.Icon icon)
            {
                icon.Save(_response.OutputStream);
            }
            private void Write(byte[] buffer)
            {
                _response.ContentLength64 = buffer.Length;
                using (Stream response = _response.OutputStream)
                {
                    response.Write(buffer, 0, buffer.Length);
                }
            }

            private void GetUser()
            {
                // Check if user is logged in (via cookies?)
                Cookie reqLogger = _request.Cookies["UserHash"];
                if (reqLogger != null)
                {
                    string hash = Convert.ToString(reqLogger.Value);
                    ulong id;
                    if ((id = GetLogged(hash)) != 0)
                    {
                        _user = new User(id);
                    }
                    else
                    {
                        _user = null;
                        Warnings.Add(new CookieNotInvalidWarning(hash));
                    }
                }
                else 
                {
                    _user = null;
                }
                GetCulture();
            }
            private void GetCulture()
            {
                // First, if logged in, use that.
                // Then, if in cookies, use that.
                // Then, if location is in request, use that.
                // ONLY then, use en-US as default.
                
                // If logged in:
                if (_user != null)
                {
                    // Get from settings. For now, we'll use en-US. Store this in cookie? 
                    // (it will be faster to get from cookie than to query db)
                    culture = new CultureInfo(_user.Preferences.Culture);
                }
                // If in cookies:
                else if (_request.Cookies["culture"] != null)
                {
                    culture = new CultureInfo(_request.Cookies["culture"].Value);
                }
                // If location in request:
                else if (false)
                {
                    // use navigator object in javascript. Store this in cookie!
                }
                // otherwise, en-US
                else
                {
                    culture = new CultureInfo("en-US");
                    // do NOT store this in cookie!
                }
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
            }

            private string ParsePath()
            {
                string path = _request.RawUrl;
                if (path.Contains("?") || path.Length < 2)
                {
                    // there will be no img
                    path = "";
                }
                return path;
            }

            private string Login(string email, string password)
            {
                try
                {
                    _user = new User(new MailAddress(email), password);
                    // Get hash
                    string hash = AddConnection(_user.UserId, _request.RemoteEndPoint);
                    Cookie logger = new Cookie("UserHash", hash);
                    _response.Cookies.Add(logger);
                    _response.AppendHeader("dest", "dashboard");
                    // If already logged in, just add remote end point:
                    return ParseQuery();
                }
                catch (Exception e)
                {
                    return LoginManager.FailLogin(e);
                }
            }
            private string Update()
            {
                switch (_dict["item"])
                {
                    case "name":
                        // Update this user's name, then respond back with success:
                        _user.UserName = _dict["userName"];
                        break;
                    case "email":
                        _user.Email = new MailAddress(_dict["email"]);
                        break;
                    case "birthday":
                        _user.BirthMonth = Convert.ToInt32(_dict["month"]);
                        _user.BirthDay = Convert.ToInt32(_dict["day"]);
                        break;
                    case "bio":
                        _user.Bio = _dict["bio"];
                        break;
                    case "delete":
                        _user.Delete();
                        Logout();
                        return LoginManager.Login();
                    // will return HERE so as to not update a null user
                    default:
                        _response.StatusCode = 404;
                        return "404";
                }
                _user.Update();
                return "200";
            }
            private string Update(Preferences preferences)
            {
                preferences.Culture = _dict["culture"];
                // preferences.Theme = Convert.ToInt32(_dict["theme"]);
                preferences.Update();
                return "200";
            }
            private string Update(EventUser evnt)
            {
                switch (_dict["item"])
                {
                    case "name":
                        break;
                    case "delete":
                        evnt.Delete();
                        return "200";
                    default:
                        _response.StatusCode = 404;
                        return "404";
                }
                evnt.Update();
                return "200";
            }
            private string Update(Group group)
            {
                switch (_dict["item"])
                {
                    case "addUser":
                        {
                            User added = new User(new MailAddress(_dict["addUserEmail"]));
                            group.Add(added);
                            break;
                        }
                    case "removeUser":
                        {
                            User removed = new User(Convert.ToUInt64(_dict["userID"]));
                            group.Remove(removed);
                            break;
                        }
                    case "removeMe":
                        {
                            group.Remove(_user);
                            break;
                        }
                    case "addEvent":
                        {
                            EventUser added = new EventUser(Convert.ToUInt64(_dict["eventID"]));
                            group.Add(added);
                            break;
                        }
                    case "removeEvent":
                        {
                            EventUser removed = new EventUser(Convert.ToUInt64(_dict["eventID"]));
                            group.Remove(removed);
                            break;
                        }
                    case "name":
                        {
                            group.Name = _dict["name"];
                            break;
                        }
                    case "delete":
                        {
                            group.Delete();
                            return "200";
                        }
                    default:
                        {
                            _response.StatusCode = 404;
                            return "404";
                        }
                }
                group.Update();
                return "200";
            }
            private string Update(Gift gift)
            {
                if (_user != null)
                {
                    gift.Name = _dict["name"];
                    gift.Description = _dict["description"];
                    gift.Url = _dict["url"];
                    gift.Cost = Convert.ToDouble(_dict["cost"] == null || _dict["cost"].Length == 0 ? "0.00" : _dict["cost"]);
                    gift.Quantity = Convert.ToUInt32(_dict["quantity"] == null || _dict["quantity"].Length == 0 ? "1" : _dict["quantity"]);
                    gift.Rating = Convert.ToDouble(_dict["rating"] == null || _dict["rating"].Length == 0 ? "0.0" : _dict["rating"]);
                    gift.ColorText = _dict["colorText"];
                    gift.Stores = _dict["stores"];
                    gift.Size = _dict["size"];
                    gift.Category = new Category(_dict["category"]);
                    // gift.DateReceived = DateTime.Parse(_dict["dateReceived"]);
                    gift.Color = _dict["color"]; // as a hex
                    gift.Update();
                    return "200";
                }
                else
                {
                    return LoginManager.Login();
                }
            }
            private string Fetch(string email)
            {
                if (email.Equals(_user.Email))
                {
                    _response.StatusCode = 200;
                    return "";
                }
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT users.UserID FROM users WHERE UserEmail = @email;";
                        cmd.Parameters.AddWithValue("@email", email);
                        cmd.Prepare();
                        using (MySqlDataReader Reader = cmd.ExecuteReader())
                        {
                            // Return true if any results
                            if (Reader.Read())
                            {
                                return (new User(Convert.ToUInt64(Reader["UserID"]))).UserName;
                            }
                            else
                            {
                                return "";
                            }
                        }
                    }
                }
            }

            private void Logout()
            {
                _response.Cookies.Add(new Cookie
                {
                    Name = "UserHash",
                    Value = "",
                    Expires = DateTime.Now.AddDays(-1d)
                });
                // If currently logged in, request will have cookie. See if cookie exists, and remove if so
                if (_request.Cookies["UserHash"] != null)
                {
                    RemoveConnection(_request.Cookies["UserHash"].Value);
                }
            }

            private bool IsLogged(string hash)
            {
                return Connections.Exists(new Predicate<Connection>((Connection con) =>
                {
                    return con.Info != null && hash.Equals(con.Info.Hash);
                }));
            }
            private ulong GetLogged(string hash)
            {
                ulong id = 0;
                Connections.Exists(new Predicate<Connection>((Connection con) =>
                {
                    if (con.Info != null && hash.Equals(con.Info.Hash))
                    {
                        id = con.Info.UserId;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }));
                return id;
            }

            private string AddConnection(ulong userId, IPEndPoint iPEndPoint)
            {
                lock (key)
                {
                    string hash = "";
                    if (!Connections.Exists(new Predicate<Connection>((Connection con) =>
                    {
                        if (con.Info != null && userId.Equals(con.Info.UserId))
                        {
                            hash = con.Info.Hash;
                            con.Ends.Add(iPEndPoint);
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    })))
                    {
                        Connection con = new Connection(userId);
                        con.Ends.Add(iPEndPoint);
                        Connections.Add(con);
                        hash = con.Info.Hash;
                    }
                    return hash;
                }
            }
            private void RemoveConnection(ulong userId)
            {
                lock(key)
                {
                    Connections.RemoveAll(new Predicate<Connection>((Connection con) =>
                    {
                        return con.Info.UserId == userId;
                    }));
                }
            }
            private void RemoveConnection(string hash)
            {
                lock(key)
                {
                    Connections.RemoveAll(new Predicate<Connection>((Connection con) =>
                    {
                        return hash.Equals(con.Info.Hash);
                    }));
                }
            }
            private static string GeneratePath(string uri)
            {
                return Directory.GetCurrentDirectory() + uri;
            }
        }
    }
}
