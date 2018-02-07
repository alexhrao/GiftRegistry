﻿using System;
using System.IO;
using System.Xml;
using System.Net.Mail;
using System.Collections.Generic;
using System.Configuration;
using GiftServer.Properties;
using GiftServer.Security;
using GiftServer.Exceptions;
using MySql.Data.MySqlClient;
using GiftServer.Server;
using System.Linq;

namespace GiftServer
{
    namespace Data
    {
        /// <summary>
        /// A single user of The GiftHub
        /// </summary>
        /// <remarks>
        /// A User is the _center_ of all operations within The GiftHub. Except for very limited circumstances, a User is required to do anything.
        /// As it's the center of any operation, the User class is quite complex, though not a "God Object" - it does not keep track of state.
        /// Note that certain methods are "lazy" - they won't populate until requested. This is done to save on unnecessary execution, and to ensure
        /// that the most recent changes are reflected.
        /// 
        /// The User class implements the ISynchronizable, IShowable, and IFetchable interfaces, which means:
        ///     - It can be changed, created, or deleted from the database
        ///     - It has an associated image
        ///     - It can be "serialized" as an XML Document
        /// </remarks>
        public class User : ISynchronizable, IShowable, IFetchable, IEquatable<User>
        {
            /// <summary>
            /// The UserID for this user
            /// </summary>
            /// <remarks>
            /// If this is 0, this is a "dead" user - an invalid user. No operations should be made on such a user,
            /// with the notable exception of Create();
            /// though an operation will NOT throw an exception if the ID is 0.
            /// </remarks>
            public ulong ID
            {
                get;
                private set;
            } = 0;
            private string name = "";
            /// <summary>
            /// The User's Name
            /// </summary>
            /// <remarks>
            /// Can contain any character, including emojis. There is no "First, Last" fields, to aid localization
            /// </remarks>
            public string Name
            {
                get
                {
                    return name;
                }
                set
                {
                    if (!String.IsNullOrWhiteSpace(value))
                    {
                        name = value;
                    }
                    else if (value == null)
                    {
                        throw new ArgumentNullException("Null User Name");
                    }
                    else
                    {
                        throw new ArgumentException("Non-Visible User Name given");
                    }
                }
            }
            private MailAddress email;
            /// <summary>
            /// The Email Address for this user
            /// </summary>
            /// <remarks>
            /// This address MUST be unique, since it's used as the Unique identifier (aside from the UserID, of course)
            /// </remarks>
            public MailAddress Email
            {
                get
                {
                    return email;
                }
                set
                {
                    if (value != null)
                    {
                        email = value;
                    }
                    else
                    {
                        throw new ArgumentNullException("Null Email");
                    }
                }
            }
            private Password password;
            /// <summary>
            /// The Password for this user.
            /// </summary>
            /// <remarks>
            /// This does NOT expose the user's actual password - this password is unavailable,
            /// since salting and hashing is done to prevent possible password leaks. However, one 
            /// CAN access the Salt, Hash, and number of iterations used via the Password, and can 
            /// validate a given password against this property.
            /// </remarks>
            public Password Password
            {
                get
                {
                    return password;
                }
                set
                {
                    if (value != null)
                    {
                        password = value;
                    }
                    else
                    {
                        throw new ArgumentNullException("Null Password");
                    }
                }
            }
            /// <summary>
            /// The birth month - we don't store their year of birth.
            /// </summary>
            public int BirthMonth = 0;
            /// <summary>
            /// The birth day - we don't store their year of birth.
            /// </summary>
            public int BirthDay = 0;
            private string bio = "";
            /// <summary>
            /// The user's "bio"
            /// </summary>
            /// <remarks>
            /// This field accepts any value.
            /// </remarks>
            public string Bio
            {
                get
                {
                    return bio;
                }
                set
                {
                    if (value != null)
                    {
                        bio = value;
                    }
                    else
                    {
                        bio = "";
                    }
                }
            }
            /// <summary>
            /// The user's preferences
            /// </summary>
            public Preferences Preferences
            {
                get;
                private set;
            }
            /// <summary>
            /// The User's URL
            /// </summary>
            /// <remarks>
            /// The URL is a Unique Identifier that, without access to the database, cannot be "reinterpreted" into anything about the user.
            /// In fact, this URL is generated at User Creation time without using _any_ user information. Indeed, not only is this locator 
            /// randomly generated, it is also cryptographically strong, though at this time that is certainly not necessary.
            /// 
            /// By definition, any character within this URL does not need to be escaped in any known HTML protocol. Furthermore, it 
            /// is gauranteed to be unique among all users.
            /// </remarks>
            public string Url
            {
                get;
                private set;
            } = "";
            /// <summary>
            /// The date this user created his or her account.
            /// </summary>
            /// <remarks>
            /// In general, this field should not be null. There exists only one case where this will not be true - 
            /// when the user has yet to be created
            /// </remarks>
            public DateTime? DateJoined
            {
                get;
                private set;
            } = null;
            private string googleId = null;
            /// <summary>
            /// The Unique GoogleID for this user.
            /// </summary>
            /// <remarks>
            /// If the user has never signed in with Google, this will be null.
            /// 
            /// See GoogleUser for more information
            /// </remarks>
            public string GoogleId
            {
                get
                {
                    return googleId;
                }
                set
                {
                    googleId = String.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            private string facebookId = null;
            /// <summary>
            /// The Unique FacebookID for this user.
            /// </summary>
            /// <remarks>
            /// If the user has never signed in with Facebook, this will be null.
            /// 
            /// See FacebookUser for more information
            /// </remarks>
            public string FacebookId
            {
                get
                {
                    return facebookId;
                }
                set
                {
                    facebookId = String.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            /// <summary>
            /// The user's gifts
            /// </summary>
            /// <remarks>
            /// This is a lazy operator; it will only populate when called upon. This ensures the latest data is fetched.
            /// </remarks>
            public List<Gift> Gifts
            {
                get
                {
                    List<Gift> _gifts = new List<Gift>();
                    if (ID != 0)
                    {
                        using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                        {
                            con.Open();
                            using (MySqlCommand cmd = new MySqlCommand())
                            {
                                cmd.Connection = con;
                                cmd.CommandText = "SELECT GiftID FROM gifts WHERE UserID = @id ORDER BY GiftRating DESC;";
                                cmd.Parameters.AddWithValue("@id", ID);
                                cmd.Prepare();
                                using (MySqlDataReader reader = cmd.ExecuteReader())
                                {
                                    // add a new gift for each id:
                                    while (reader.Read())
                                    {
                                        _gifts.Add(new Gift(Convert.ToUInt64(reader["GiftID"])));
                                    }
                                }
                            }
                        }
                    }
                    return _gifts;
                }
            }
            /// <summary>
            /// The user's groups
            /// </summary>
            /// <remarks>
            /// This is a lazy operator; it will only populate when called upon. This ensures the latest data is fetched.
            /// </remarks>
            public List<Group> Groups
            {
                get
                {
                    List<Group> _groups = new List<Group>();
                    if (ID != 0)
                    {
                        using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                        {
                            con.Open();
                            using (MySqlCommand cmd = new MySqlCommand())
                            {
                                cmd.Connection = con;
                                cmd.CommandText = "SELECT GroupID FROM groups_users WHERE UserID = @id UNION SELECT GroupID FROM groups WHERE AdminID = @id;";
                                cmd.Parameters.AddWithValue("@id", ID);
                                cmd.Prepare();
                                using (MySqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        _groups.Add(new Group(Convert.ToUInt64(reader["GroupID"])));
                                    }
                                }
                            }
                        }
                    }
                    _groups.Sort((a, b) => a.Name.CompareTo(b.Name));
                    return _groups;
                }
            }
            /// <summary>
            /// The user's events
            /// </summary>
            /// <remarks>
            /// This is a lazy operator; it will only populate when called upon. This ensures the latest data is fetched.
            /// </remarks>
            public List<Event> Events
            {
                get
                {
                    List<Event> _events = new List<Event>();
                    if (ID != 0)
                    {
                        using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                        {
                            con.Open();
                            using (MySqlCommand cmd = new MySqlCommand())
                            {
                                cmd.Connection = con;
                                cmd.CommandText = "SELECT EventID FROM user_events WHERE UserID = @uid;";
                                cmd.Parameters.AddWithValue("@uid", ID);
                                cmd.Prepare();
                                using (MySqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        _events.Add(new Event(Convert.ToUInt64(reader["EventID"])));
                                    }
                                }
                            }
                        }
                    }
                    return _events;
                }
            }
            /// <summary>
            /// Initializes a new User
            /// </summary>
            /// <param name="id">The UserID</param>
            /// <remarks>
            /// This assumes that the ID exists; if it doesn't, a UserNotFoundException is thrown.
            /// </remarks>
            /// <exception cref="UserNotFoundException">Thrown if user can't be found</exception>
            /// <exception cref="InvalidPasswordException">Thrown if given password is invalid</exception>
            public User(ulong id)
            {
                Synchronize(id);
            }
            /// <summary>
            /// Initializes an existing user with email address in email
            /// </summary>
            /// <param name="email">The User's Email</param>
            /// <remarks>
            /// This assumes a user with this email already exists; failure to find one results in a UserNotFoundException
            /// </remarks>
            public User(MailAddress email)
            {
                if (email == null)
                {
                    throw new ArgumentNullException("Email was null");
                }
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT users.UserID FROM users WHERE UserEmail = @eml;";
                        cmd.Parameters.AddWithValue("@eml", email.Address);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Synchronize(Convert.ToUInt64(reader["UserID"]));
                            }
                            else
                            {
                                throw new UserNotFoundException(email);
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// Initializes a new User
            /// </summary>
            /// <param name="hash">The UserURL</param>
            /// <remarks>
            /// The URL for this user, if this URL isn't found, a UserNotFoundException is thrown
            /// </remarks>
            public User(string hash)
            {
                if (hash == null)
                {
                    throw new ArgumentNullException("Hash was null");
                }
                else if (String.IsNullOrWhiteSpace(hash))
                {
                    throw new ArgumentException("Hash was clear");
                }
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT users.UserID FROM users WHERE UserURL = @url;";
                        cmd.Parameters.AddWithValue("@url", hash);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Synchronize(Convert.ToUInt64(reader["UserID"]));
                            }
                            else
                            {
                                throw new UserNotFoundException(hash);
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// Initialize a user from an OAuth Sign In
            /// </summary>
            /// <param name="user">The OAuthUser (created from the authentication token)</param>
            /// <param name="sender">A function that will send the reset email to a specified MailAddress</param>
            public User(OAuthUser user, Action<MailAddress> sender)
            {
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        switch (user)
                        {
                            case GoogleUser g:
                                cmd.CommandText = "SELECT users.UserID FROM users WHERE users.UserGoogleID = @oid;";
                                break;
                            case FacebookUser f:
                                cmd.CommandText = "SELECT users.UserID FROM users WHERE users.UserFacebookID = @oid;";
                                break;
                            default:
                                throw new ArgumentException("Unkown OAuth type: " + user.GetType().Name);
                        }
                        cmd.Parameters.AddWithValue("@oid", user.OAuthId);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // User exists; fetch normally and move on
                                Synchronize(Convert.ToUInt64(reader["UserID"]));
                                Update(user);
                            }
                            else
                            {
                                // We have a new user - check and see if email is already here. If so, just update that user's info!
                                if (!Search(user))
                                {
                                    // We have a new user; copy over information and CREATE()
                                    Create(user);
                                    // Send password reset email
                                    sender(Email);
                                }
                            }
                        }
                    }
                }
            }
            private bool Search(OAuthUser user)
            {
                // Search for user via email - if found, add OAuthID and return true; otherwise, false
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT users.UserID FROM users WHERE users.UserEmail = @eml;";
                        cmd.Parameters.AddWithValue("@eml", user.Email);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            // If found, get id:
                            if (reader.Read())
                            {
                                ulong uid = Convert.ToUInt64(reader["UserID"]);
                                Synchronize(uid);
                                switch (user)
                                {
                                    case GoogleUser g:
                                        GoogleId = g.OAuthId;
                                        break;
                                    case FacebookUser f:
                                        FacebookId = f.OAuthId;
                                        break;
                                    default:
                                        throw new ArgumentException("Unkown OAuth type: " + user.GetType().Name);
                                }
                                Update();
                                return true;
                            }
                            else
                            {
                                // Not found - false!
                                return false;
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// Fetches a user with the specified email and password
            /// </summary>
            /// <param name="email">The user's email</param>
            /// <param name="password">The plaintext form of the user's password</param>
            /// <remarks>
            /// Even if the email is found, if the veracity of the password cannot be found,
            /// this will throw an InvalidPasswordException
            /// </remarks>
            public User(MailAddress email, string password)
            {
                // If this is called, the user already exists in DB; fetch. If it can't find it, throw UserNotFoundException. 
                // If found, but password mismatch, throw InvalidPasswordException.
                if (password == null)
                {
                    throw new ArgumentNullException("Password is null");
                }
                else if (password.Length == 0)
                {
                    throw new ArgumentException("Password is 0 length");
                }
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT users.UserID FROM users WHERE UserEmail = @eml;";
                        cmd.Parameters.AddWithValue("@eml", email.Address);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Synchronize(Convert.ToUInt64(reader["UserID"]), password);
                            }
                            else
                            {
                                throw new UserNotFoundException(email);
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// Creates a *new* user with email and password
            /// </summary>
            /// <param name="email">The user's email</param>
            /// <param name="password">The user's password, *_hashed_*</param>
            public User(MailAddress email, Password password)
            {
                Email = email;
                Password = password;
            }
            /// <summary>
            /// Resets their password
            /// </summary>
            /// <param name="password">The new password</param>
            /// <param name="sender">A sender that will generate the notification for this user.</param>
            /// <returns>A status of whether or not it successfully reset the user's password</returns>
            public bool UpdatePassword(string password, Action<MailAddress, User> sender)
            {
                if (ID == 0)
                {
                    return false;
                }
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        Password = new Password(password);
                        cmd.Connection = con;
                        cmd.CommandText = "UPDATE passwords SET PasswordHash = @hsh, PasswordSalt = @slt, PasswordIter = @itr WHERE UserID = @uid;";
                        cmd.Parameters.AddWithValue("@hsh", Password.Hash);
                        cmd.Parameters.AddWithValue("@slt", Password.Salt);
                        cmd.Parameters.AddWithValue("@itr", Password.Iterations);
                        cmd.Parameters.AddWithValue("@uid", ID);
                        cmd.Prepare();
                        cmd.ExecuteNonQuery();
                    }
                }
                sender(Email, this);
                return true;
            }
            private void Synchronize(ulong id, string password)
            {
                Synchronize(id);
                if (!Password.Verify(password))
                {
                    throw new InvalidPasswordException();
                }
            }
            private void Synchronize(ulong id)
            {
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT users.*, passwords.PasswordHash, passwords.PasswordSalt, passwords.PasswordIter "
                                        + "FROM users "
                                        + "LEFT JOIN passwords ON passwords.UserID = users.UserID "
                                        + "WHERE users.UserID = @id;";
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                ID = id;
                                Name = Convert.ToString(reader["UserName"]);
                                Email = new MailAddress(Convert.ToString(reader["UserEmail"]));
                                if (!String.IsNullOrEmpty(Convert.ToString(reader["PasswordHash"])))
                                {
                                    Password = new Password(Convert.ToString(reader["PasswordHash"]),
                                                        Convert.ToString(reader["PasswordSalt"]),
                                                        Convert.ToInt32(reader["PasswordIter"]));
                                }
                                BirthDay = Convert.ToInt32(reader["UserBirthDay"]);
                                BirthMonth = Convert.ToInt32(reader["UserBirthMonth"]);
                                DateJoined = (DateTime)(reader["TimeCreated"]);
                                Bio = Convert.ToString(reader["UserBio"]);
                                Url = Convert.ToString(reader["UserURL"]);
                                GoogleId = Convert.ToString(reader["UserGoogleID"]);
                                FacebookId = Convert.ToString(reader["UserFacebookID"]);

                                Preferences = new Preferences(this);
                            }
                            else
                            {
                                throw new UserNotFoundException(id);
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// Creates the user in the database.
            /// </summary>
            /// <remarks>
            /// If the email is already taken, this will throw a DuplicatUserException
            /// </remarks>
            public void Create()
            {
                if ((BirthDay == 0 ^ BirthMonth == 0))
                {
                    throw new ArgumentException("Birth Month is " + BirthMonth.ToString() + " But Birth Day is " + BirthDay.ToString());
                }
                // Validate Name
                Name = name;
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        // Check if email present:
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT UserID FROM users WHERE UserEmail = @email OR UserFacebookID = @fid OR UserGoogleID = @gid;";
                        cmd.Parameters.AddWithValue("@email", Email.Address);
                        cmd.Parameters.AddWithValue("@fid", FacebookId);
                        cmd.Parameters.AddWithValue("@gid", GoogleId);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // User already exists; throw exception:
                                throw new DuplicateUserException(Email);
                            }
                        }
                    }
                    bool isUnique = false;
                    while (!isUnique)
                    {
                        Password url = new Password(Email.Address);
                        Url = url.Hash.Replace("+", "0").Replace("/", "A").Replace("=", "R");
                        using (MySqlCommand cmd = new MySqlCommand())
                        {
                            cmd.Connection = con;
                            cmd.CommandText = "SELECT users.UserID FROM users WHERE users.UserURL = @url;";
                            cmd.Parameters.AddWithValue("@url", Url);
                            cmd.Prepare();
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                isUnique = !reader.HasRows;
                            }
                        }
                    }
                    // Look and see
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "INSERT INTO users (UserName, UserEmail, UserBirthMonth, UserBirthDay, UserBio, UserURL, UserGoogleID, userFacebookID) "
                            + "VALUES (@name, @email, @bmonth, @bday, @bio, @url, @gid, @fid);";
                        cmd.Parameters.AddWithValue("@name", Name);
                        cmd.Parameters.AddWithValue("@email", Email);
                        cmd.Parameters.AddWithValue("@bmonth", BirthMonth);
                        cmd.Parameters.AddWithValue("@bday", BirthDay);
                        cmd.Parameters.AddWithValue("@bio", Bio);
                        cmd.Parameters.AddWithValue("@url", Url);
                        cmd.Parameters.AddWithValue("@gid", GoogleId);
                        cmd.Parameters.AddWithValue("@fid", FacebookId);
                        cmd.Prepare();
                        cmd.ExecuteNonQuery();
                        ID = Convert.ToUInt64(cmd.LastInsertedId);
                    }
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        // Create new password:
                        cmd.CommandText = "INSERT INTO passwords (UserID, PasswordHash, PasswordSalt, PasswordIter) VALUES (@uid, @hsh, @slt, @itr);";
                        cmd.Parameters.AddWithValue("@uid", ID);
                        cmd.Parameters.AddWithValue("@hsh", Password.Hash);
                        cmd.Parameters.AddWithValue("@slt", Password.Salt);
                        cmd.Parameters.AddWithValue("@itr", Password.Iterations);
                        cmd.Prepare();
                    }
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT TimeCreated FROM users WHERE UserID = @id;";
                        cmd.Parameters.AddWithValue("@id", ID);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                DateJoined = (DateTime)(reader["TimeCreated"]);
                            }
                        }
                    }
                    Preferences = new Preferences(this);
                    Preferences.Create();
                }
            }
            private void Create(OAuthUser info)
            {
                Email = info.Email;
                Name = info.Name;

                switch (info)
                {
                    case GoogleUser g:
                        GoogleId = g.OAuthId;
                        break;
                    case FacebookUser f:
                        FacebookId = f.OAuthId;
                        break;
                    default:
                        throw new ArgumentException("Unkown OAuth type: " + info.GetType().Name);
                }
                Password = new Password(info.OAuthId);
                Create();
                // Change locale and update:
                // Find nearest locale, update with that:
                Preferences.Culture = Controller.ParseCulture(info.Locale);
                // Save image:
                SaveImage(info.Picture);
            }
            /// <summary>
            /// Updates the user
            /// </summary>
            /// <remarks>
            /// If the ID is 0, this will instead _create_ a new user
            /// </remarks>
            public void Update()
            {
                if ((BirthDay == 0 ^ BirthMonth == 0))
                {
                    throw new ArgumentException("Birth Month is " + BirthMonth.ToString() + " But Birth Day is " + BirthDay.ToString());
                }
                if (ID == 0)
                {
                    // User does not exist - create new one instead.
                    Create();
                    return;
                }
                Preferences.Update();
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        // Check if email present:
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT UserID FROM users WHERE UserID <> @uid AND (UserEmail = @email OR UserFacebookID = @fid OR UserGoogleID = @gid);";
                        cmd.Parameters.AddWithValue("@uid", ID);
                        cmd.Parameters.AddWithValue("@email", Email.Address);
                        cmd.Parameters.AddWithValue("@fid", FacebookId);
                        cmd.Parameters.AddWithValue("@gid", GoogleId);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // User already exists; throw exception:
                                throw new DuplicateUserException(Email);
                            }
                        }
                    }
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        // Update user information
                        cmd.CommandText = "UPDATE users "
                            + "SET UserName = @name, "
                            + "UserEmail = @email, "
                            + "UserBio = @bio, "
                            + "UserBirthMonth = @bmonth, "
                            + "UserBirthDay = @bday, "
                            + "UserGoogleID = @gid, "
                            + "UserFacebookID = @fid "
                            + "WHERE UserID = @uid;";
                        cmd.Parameters.AddWithValue("@name", Name);
                        cmd.Parameters.AddWithValue("@email", Email);
                        cmd.Parameters.AddWithValue("@bio", Bio);
                        cmd.Parameters.AddWithValue("@bmonth", BirthMonth);
                        cmd.Parameters.AddWithValue("@bday", BirthDay);
                        cmd.Parameters.AddWithValue("@gid", GoogleId);
                        cmd.Parameters.AddWithValue("@fid", FacebookId);
                        cmd.Parameters.AddWithValue("@uid", ID);
                        cmd.Prepare();
                        cmd.ExecuteNonQuery();
                    }
                    // Only way to update password is through password reset, so no need here
                }
            }
            private void Update(OAuthUser user)
            {
                bool isChanged = false;
                if (user.Name != Name)
                {
                    Name = user.Name;
                    isChanged = true;
                }
                if (Controller.ParseCulture(user.Locale) != Preferences.Culture)
                {
                    Preferences.Culture = Controller.ParseCulture(user.Locale);
                    isChanged = true;
                }
                if (isChanged)
                {
                    Update();
                }
            }
            /// <summary>
            /// Deletes a user from the database
            /// </summary>
            /// <remarks>
            /// This is a permanent change, and will delete all memberships, gifts, and preferences.
            /// </remarks>
            public void Delete()
            {
                if (ID != 0)
                {
                    // Check if admin of any group
                    if (Groups.Exists(g => g.Admin.ID == ID))
                    {
                        throw new InvalidOperationException("User is still an admin of one or more groups");
                    }
                    RemoveImage();
                    using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                    {
                        con.Open();
                        // Remove image:
                        RemoveImage();
                        // Remove preferences
                        Preferences.Delete();
                        // Delete records of events:
                        foreach (Event e in Events)
                        {
                            e.Delete();
                        }
                        foreach (Gift gift in Gifts)
                        {
                            gift.Delete();
                        }
                        foreach (Group group in Groups)
                        {
                            group.Remove(this);
                        }
                        // No need to delete from groups; guaranteed to NOT be admin of any
                        // Delete from Passwordresets
                        using (MySqlCommand cmd = new MySqlCommand())
                        {
                            cmd.Connection = con;
                            cmd.CommandText = "DELETE FROM passwordresets WHERE UserID = @id;";
                            cmd.Parameters.AddWithValue("@id", ID);
                            cmd.Prepare();
                            cmd.ExecuteNonQuery();
                        }
                        // Delete from passwords
                        using (MySqlCommand cmd = new MySqlCommand())
                        {
                            cmd.Connection = con;
                            cmd.CommandText = "DELETE FROM passwords WHERE UserID = @id;";
                            cmd.Parameters.AddWithValue("@id", ID);
                            cmd.Prepare();
                            cmd.ExecuteNonQuery();
                        }
                        // Delete from users
                        using (MySqlCommand cmd = new MySqlCommand())
                        {
                            cmd.Connection = con;
                            cmd.CommandText = "DELETE FROM users WHERE UserID = @id;";
                            cmd.Parameters.AddWithValue("@id", ID);
                            cmd.Prepare();
                            cmd.ExecuteNonQuery();
                        }
                        ID = 0;
                    }
                }
            }
            /// <summary>
            /// Saves an image for this user.
            /// </summary>
            /// <param name="contents">The new image, regardless of original format</param>
            /// <remarks>
            /// If the contents are null or empty, this is identical to calling RemoveImage()
            /// </remarks>
            public void SaveImage(byte[] contents)
            {
                if (ID == 0)
                {
                    throw new InvalidOperationException("Cannot save image of ID-less user");
                }
                if (contents == null || contents.Length == 0)
                {
                    RemoveImage();
                }
                else
                {
                    ImageProcessor processor = new ImageProcessor(contents);
                    File.WriteAllBytes(Directory.GetCurrentDirectory() + "/resources/images/users/User" + ID + Constants.ImageFormat, processor.Data);
                }
            }
            /// <summary>
            /// Removes an associated image
            /// </summary>
            /// <remarks>
            /// The user's image will be replaced with default.png.
            /// </remarks>
            public void RemoveImage()
            {
                if (ID == 0)
                {
                    throw new InvalidOperationException("Cannot remove image of ID-less user");
                }
                if (File.Exists(Directory.GetCurrentDirectory() + "/resources/images/users/User" + ID + Constants.ImageFormat))
                {
                    File.Delete(Directory.GetCurrentDirectory() + "/resources/images/users/User" + ID + Constants.ImageFormat);
                }
            }
            /// <summary>
            /// Returns the path for this user's image
            /// </summary>
            /// <returns>A qualified path for this user's image</returns>
            /// <remarks>
            /// The qualified path is with respect to the _server's_ root directory, which is *not necessarily 'C:\' or '/'*
            /// </remarks>
            public string GetImage()
            {
                if (ID == 0)
                {
                    throw new InvalidOperationException("Cannot retrieve image of ID-less user");
                }
                return GetImage(ID);
            }
            /// <summary>
            /// Gets the image for any user
            /// </summary>
            /// <param name="userID">The user ID for this given user</param>
            /// <returns>A qualified path for this user's image. See GetImage() for more information</returns>
            public static string GetImage(ulong userID)
            {
                if (userID == 0)
                {
                    throw new ArgumentException("Cannot retrieve image of ID-less user", nameof(userID));
                }
                // Build path:
                string path = Directory.GetCurrentDirectory() + "/resources/images/users/User" + userID + Constants.ImageFormat;
                // if file exists, return path. Otherwise, return default
                // Race condition, but I don't know how to solve (yet)
                if (File.Exists(path))
                {
                    return "resources/images/users/User" + userID + Constants.ImageFormat;
                }
                else
                {
                    return "resources/images/users/default" + Constants.ImageFormat;
                }
            }

            /// <summary>
            /// Reserve ONE of a given gift;
            /// </summary>
            /// <param name="gift">The gift to reserve</param>
            public void Reserve(Gift gift)
            {
                if (gift == null)
                {
                    throw new ArgumentNullException(nameof(gift));
                }
                Reservation res = new Reservation(this, gift);
                res.Create();
            }

            /// <summary>
            /// Reserve |amount| of gift.
            /// </summary>
            /// <param name="gift">The gift to reserve</param>
            /// <param name="amount">The number of reservations to make</param>
            /// <returns>Number of successfully reserved gifts</returns>
            public int Reserve(Gift gift, int amount)
            {
                if (gift == null)
                {
                    throw new ArgumentNullException(nameof(gift));
                }
                if (amount < 0)
                {
                    return Release(gift, -amount);
                }
                for (int c = 0; c < amount; c++)
                {
                    try
                    {
                        Reserve(gift);
                    }
                    catch (ReservationOverflowException)
                    {
                        return c;
                    }
                }
                return amount;
            }

            /// <summary>
            /// Release ONE of the gifts (if multiple are reserved)
            /// </summary>
            /// <param name="gift">The gift to release</param>
            public void Release(Gift gift)
            {
                if (gift == null)
                {
                    throw new ArgumentNullException(nameof(gift));
                }
                Reservation res = null;
                if (gift.Reservations.FindAll(r => r.User.ID == ID).Count > 0)
                {
                    res = gift.Reservations.FindAll(r => r.User.ID == ID)[0];
                    res.Delete();
                }
            }
            /// <summary>
            /// Release a specified amount of gifts.
            /// </summary>
            /// <param name="gift">The gift to release</param>
            /// <param name="amount">The number of releases to perform</param>
            /// <returns>The actual number of releases successfully performed</returns>
            public int Release(Gift gift, int amount)
            {
                if (gift == null)
                {
                    throw new ArgumentNullException(nameof(gift));
                }
                if (amount < 0)
                {
                    return Reserve(gift, -amount);
                }
                int counter = 0;
                foreach (Reservation res in gift.Reservations.FindAll(r => r.User.ID == ID))
                {
                    Release(gift);
                    counter++;
                    if (counter >= amount)
                    {
                        break;
                    }
                }
                return counter;
            }

            /// <summary>
            /// Mark a gift as purchased
            /// </summary>
            /// <param name="gift">The gift to purchase</param>
            public void Purchase(Gift gift)
            {
                if (gift == null)
                {
                    throw new ArgumentNullException(nameof(gift));
                }
                Reservation res = null;
                if (gift.Reservations.FindAll(r => r.User.ID == ID && !r.IsPurchased).Count > 0)
                {
                    res = gift.Reservations.FindAll(r => r.User.ID == ID && !r.IsPurchased)[0];
                    res.IsPurchased = true;
                    res.Update();
                }
            }
            /// <summary>
            /// Purchase a given number of gifts
            /// </summary>
            /// <param name="gift">The gift to purchase</param>
            /// <param name="amount">The number of gifts to purchase</param>
            public void Purchase(Gift gift, int amount)
            {
                if (gift == null)
                {
                    throw new ArgumentNullException(nameof(gift));
                }
                if (amount < 0)
                {
                    Return(gift, -amount);
                }
                for (int i = 0; i < amount; i++)
                {
                    Purchase(gift);
                }
            }

            /// <summary>
            /// Unmark as purchased, but it is still reserved!
            /// </summary>
            /// <param name="gift">The gift to be returned</param>
            public void Return(Gift gift)
            {
                Reservation res = null;
                List<Reservation> reservations = gift.Reservations.FindAll(r => r.User.ID == ID && r.IsPurchased).OrderBy(r => r.PurchaseDate).ToList();
                if (reservations.Count > 0)
                {
                    res = reservations[0];
                    res.IsPurchased = false;
                    res.Update();
                }
            }
            /// <summary>
            /// Return the specified number of gifts
            /// </summary>
            /// <param name="gift">The gift to return</param>
            /// <param name="amount">The number of gifts to return</param>
            public void Return(Gift gift, int amount)
            {
                if (gift == null)
                {
                    throw new ArgumentNullException(nameof(gift));
                }
                if (amount < 0)
                {
                    Purchase(gift, -amount);
                }
                for (int i = 0; i < amount; i++)
                {
                    Return(gift);
                }
            }

            // NOTE: In all Get*, this is viewer!
            // In other words, Get* will get all * owned by target and viewable by this
            /// <summary>
            /// Get all gifts viewable by this user
            /// </summary>
            /// <param name="target">The owner</param>
            /// <returns>The list of viewable gifts</returns>
            /// <remarks>
            /// Like other Get* operations, _this_ is considered the viewer, and _target_ is the owner.
            /// 
            /// In other words, GetGifts will get all gifts owned by the target &amp; viewable by this user. The target
            /// will have at least one group in common with this user, *and* have marked one gift viewable by said group
            /// for this to happen.
            /// </remarks>
            public List<Gift> GetGifts(User target)
            {
                if (target == null)
                {
                    throw new ArgumentNullException("Target in GetGifts was null");
                }
                // For each gift owned by target, iterate over its groups until we find one in common with ours!
                List<Gift> gifts = new List<Gift>();
                foreach (Gift gift in target.Gifts)
                {
                    foreach (Group g in gift.Groups)
                    {
                        if (Groups.Exists(x => x.ID == g.ID))
                        {
                            // Add and break
                            gifts.Add(gift);
                            break;
                        }
                    }
                }
                return gifts;
            }

            /// <summary>
            /// Get all gifts viewable by this group and owned by this user
            /// </summary>
            /// <param name="viewer">The group that is viewing the gifts of this user</param>
            /// <returns>A list of gifts</returns>
            public List<Gift> GetGifts(Group viewer)
            {
                if (viewer == null)
                {
                    throw new ArgumentNullException("Viewer in GetGifts was null");
                }
                return Gifts.Where(gift => gift.Groups.Exists(group => group.ID == viewer.ID)).ToList();
            }
            /// <summary>
            /// Get all groups viewable by this user
            /// </summary>
            /// <param name="target">The owner</param>
            /// <returns>The list of viewable groups</returns>
            /// <remarks>
            /// Like other Get* operations, _this_ is considered the viewer, and _target_ is the owner.
            /// 
            /// In other words, GetGroups will get all groups that target and this have in common.
            /// </remarks>
            public List<Group> GetGroups(User target)
            {
                if (target == null)
                {
                    throw new ArgumentNullException("Target in GetGroups was null");
                }
                List<Group> groups = new List<Group>();
                // For each group in target, see if GroupID is found in ours
                foreach (Group g in target.Groups)
                {
                    if (Groups.Exists(x => x.ID == g.ID))
                    {
                        groups.Add(g);
                    }
                }
                return groups;
            }
            /// <summary>
            /// Get all events viewable by this user
            /// </summary>
            /// <param name="target">The owner</param>
            /// <returns>The list of viewable events</returns>
            /// <remarks>
            /// Like other Get* operations, _this_ is considered the viewer, and _target_ is the owner.
            /// 
            /// In other words, GetEvents will get all Events owned by the target &amp; viewable by this user. The target
            /// will have at least one group in common with this user, *and* have marked one Event viewable by said group
            /// for this to happen.
            /// </remarks>
            public List<Event> GetEvents(User target)
            {
                if (target == null)
                {
                    throw new ArgumentNullException("Target in GetEvents was null");
                }
                // For each event owned by target, see shared groups. If ANY of those shared groups are common with us, add!
                List<Event> events = new List<Event>();
                foreach (Event e in target.Events)
                {
                    // Iterate over the groups, find if any are common. If so, break (we found the group!)
                    foreach (Group g in e.Groups)
                    {
                        if (Groups.Exists(x => x.ID == g.ID))
                        {
                            // Add to group and break!
                            events.Add(e);
                            break;
                        }
                    }
                }
                return events;
            }
            /// <summary>
            /// Get all events owned by this user and visible to the given group
            /// </summary>
            /// <param name="viewer">The group that wants to view events from this user</param>
            /// <returns>A list of viewable events</returns>
            public List<Event> GetEvents(Group viewer)
            {
                if (viewer == null)
                {
                    throw new ArgumentNullException("Group in GetEvents was null");
                }
                return Events.Where(e => e.Groups.Exists(g => g.ID == viewer.ID)).ToList();
            }
            /// <summary>
            /// Add an OAuth token for this user
            /// </summary>
            /// <remarks>
            /// This will allow the user to login with said service
            /// </remarks>
            /// <param name="token">The OAuth token to add</param>
            public void AddOAuth(OAuthUser token)
            {
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        switch (token)
                        {
                            case GoogleUser g:
                                cmd.CommandText = "SELECT users.UserID FROM users WHERE UserGoogleID = @oid;";
                                break;
                            case FacebookUser f:
                                cmd.CommandText = "SELECT users.UserID FROM users WHERE UserFacebookID = @oid;";
                                break;
                        }
                        cmd.Parameters.AddWithValue("@oid", token.OAuthId);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                // We are good to go! Nobody has this ID
                                switch (token)
                                {
                                    case GoogleUser g:
                                        GoogleId = g.OAuthId;
                                        break;
                                    case FacebookUser f:
                                        FacebookId = f.OAuthId;
                                        break;
                                }
                                Update();
                            }
                            else
                            {
                                throw new DuplicateUserException(token);
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// Remove an OAuth token for this user
            /// </summary>
            /// <remarks>
            /// This will bar the user from loggin in with said service
            /// </remarks>
            /// <param name="token">The OAuth token to remove</param>
            public void RemoveOAuth(OAuthUser token)
            {
                switch (token)
                {
                    case GoogleUser g:
                        GoogleId = null;
                        break;
                    case FacebookUser f:
                        FacebookId = null;
                        break;
                }
                Update();
            }
            /// <summary>
            /// Checks if this user is equal to the given object
            /// </summary>
            /// <param name="obj">The object to compare</param>
            /// <returns>A boolean flag - false if null, not a user, or </returns>
            public override bool Equals(object obj)
            {
                if (obj != null && obj is User u)
                {
                    return Equals(u);
                }
                else
                {
                    return false;
                }
            }
            /// <summary>
            /// Checks if this user is equal to the given user
            /// </summary>
            /// <param name="user"></param>
            /// <returns></returns>
            public bool Equals(User user)
            {
                return user != null && ID == user.ID;
            }
            /// <summary>
            /// Overrides the hash code operator
            /// </summary>
            /// <returns>The hash for this user</returns>
            public override int GetHashCode()
            {
                return ID.GetHashCode();
            }
            /// <summary>
            /// "Serializes" the User as an XML Document
            /// </summary>
            /// <returns>An XmlDocument with all information about this user enclosed</returns>
            /// <remarks>
            /// This is the "root" of all fetch operations; the User class is the only one allowed to 
            /// traverse &amp; _expand all of its elements_. While this sounds like a lot of data, since it's just textual information\
            /// (no images), the data sent is always quite minimal.
            /// 
            /// This method returns an XML Document with the following fields:
            ///     - userId: The user's ID
            ///     - userName: The user's name
            ///     - email: The User's email
            ///     - birthMonth: The User's birth month
            ///     - birthYear: The User's birth year
            ///     - bio: The User's biography
            ///     - dateJoined: The date this user joined, encoded as "yyyy-MM-dd"
            ///     - image: The qualified path for this user's image
            ///     - groups: An expanded list of all this user's groups.
            ///         - Note that each child of _groups_ is a _group_ element.
            ///     - gifts: An expanded list of all this user's gifts.
            ///         - Note that each child of _gifts_ is a _gift_ element.
            ///     - events: An expanded list of all this user's events.
            ///         - Note that each child of _events_ is an _event_ element.
            ///     - preferences: This user's preferences
            ///     
            /// This is all contained within a _user_ container.
            /// </remarks>
            public XmlDocument Fetch()
            {
                XmlDocument info = new XmlDocument();
                XmlElement container = info.CreateElement("user");
                info.AppendChild(container);

                XmlElement id = info.CreateElement("userId");
                id.InnerText = ID.ToString();
                XmlElement userName = info.CreateElement("userName");
                userName.InnerText = (Name);
                XmlElement email = info.CreateElement("email");
                email.InnerText = Email.Address;
                // XmlElement password = Password.Fetch().DocumentElement;
                XmlElement birthMonth = info.CreateElement("birthMonth");
                birthMonth.InnerText = BirthMonth.ToString();
                XmlElement birthDay = info.CreateElement("birthDay");
                birthDay.InnerText = BirthDay.ToString();
                XmlElement bio = info.CreateElement("bio");
                bio.InnerText = Bio;
                XmlElement dateJoined = info.CreateElement("dateJoined");
                dateJoined.InnerText = (DateJoined.Value.ToString("yyyy-MM-dd"));
                XmlElement image = info.CreateElement("image");
                image.InnerText = GetImage();
                XmlElement groups = info.CreateElement("groups");
                foreach (Group group in Groups)
                {
                    groups.AppendChild(info.ImportNode(group.Fetch().DocumentElement, true));
                }
                XmlElement events = info.CreateElement("events");
                foreach (Event evnt in Events)
                {
                    events.AppendChild(info.ImportNode(evnt.Fetch().DocumentElement, true));
                }
                XmlElement gifts = info.CreateElement("gifts");
                foreach (Gift gift in Gifts)
                {
                    gifts.AppendChild(info.ImportNode(gift.Fetch().DocumentElement, true));
                }

                container.AppendChild(id);
                container.AppendChild(userName);
                container.AppendChild(email);
                container.AppendChild(birthMonth);
                container.AppendChild(birthDay);
                container.AppendChild(bio);
                container.AppendChild(dateJoined);
                container.AppendChild(image);
                container.AppendChild(groups);
                container.AppendChild(gifts);
                container.AppendChild(events);
                container.AppendChild(info.ImportNode(Preferences.Fetch().DocumentElement, true));

                return info;
            }
        }
    }
}