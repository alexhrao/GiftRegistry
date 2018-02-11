﻿using GiftServer.Properties;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Xml;

namespace GiftServer
{
    namespace Data
    {
        /// <summary>
        /// A single gift
        /// </summary>
        /// <remarks>
        /// A gift is owned by a specific user, and has a great deal of properties (color, quantity, etc.).
        /// </remarks>
        public class Gift : ISynchronizable, IShowable, IFetchable, IEquatable<Gift>
        {
            /// <summary>
            /// The GiftID
            /// </summary>
            /// <remarks>
            /// A GiftID means a deleted gift (or one never created).
            /// </remarks>
            public ulong ID
            {
                get;
                private set;
            } = 0;
            /// <summary>
            /// The owner of this gift
            /// </summary>
            public User User;
            /// <summary>
            /// The name of this gift
            /// </summary>
            public string Name;
            /// <summary>
            /// This gift's description
            /// </summary>
            public string Description = "";
            /// <summary>
            /// The URL associated with this gift
            /// </summary>
            public string Url = "";
            /// <summary>
            /// The Cost of this gift, as a double.
            /// </summary>
            public double Cost = 0.00;
            /// <summary>
            /// Stores this gift is sold at
            /// </summary>
            public string Stores = "";
            /// <summary>
            /// The number of gifts desired
            /// </summary>
            /// <remarks>
            /// If no number (or a number less than 1) is given, 1 is assumed.
            /// </remarks>
            public uint Quantity
            {
                get
                {
                    return quantity;
                }
                set
                {
                    if (value < 1)
                    {
                        quantity = 1;
                    }
                    else
                    {
                        quantity = value;
                    }
                }
            }
            private uint quantity = 1;
            /// <summary>
            /// The color, as HEX (without the #)
            /// </summary>
            public string Color = "000000";
            /// <summary>
            /// A description text for this color
            /// </summary>
            public string ColorText = "";
            /// <summary>
            /// The size of this gift
            /// </summary>
            public string Size = "";
            /// <summary>
            /// The category this gift fits under
            /// </summary>
            public Category Category;
            /// <summary>
            /// This gift's rating, between 0 and 5 only.
            /// </summary>
            public double Rating
            {
                get
                {
                    return rating;
                }
                set
                {
                    if (value > 5)
                    {
                        rating = 5.0;
                    }
                    else if (value < 0)
                    {
                        rating = 0.0;
                    }
                    else
                    {
                        rating = value;
                    }
                }
            }
            private double rating = 0.00;
            /// <summary>
            /// The time this gift was created
            /// </summary>
            public DateTime? TimeStamp = null;
            /// <summary>
            /// The time this gift was received
            /// </summary>
            public DateTime? DateReceived = null;
            /// <summary>
            /// A List of reservations for this gift
            /// </summary>
            public List<Reservation> Reservations
            {
                get
                {
                    List<Reservation> _reservations = new List<Reservation>();
                    using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                    {
                        con.Open();
                        using (MySqlCommand cmd = new MySqlCommand())
                        {
                            cmd.Connection = con;
                            cmd.CommandText = "SELECT ReservationID FROM reservations WHERE GiftID = @gid;";
                            cmd.Parameters.AddWithValue("@gid", ID);
                            cmd.Prepare();
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    _reservations.Add(new Reservation(Convert.ToUInt64(reader["ReservationID"])));
                                }
                            }
                        }
                    }
                    return _reservations;
                }
            }
            /// <summary>
            /// All the groups this gift is viewable to
            /// </summary>
            public List<Group> Groups
            {
                get
                {
                    List<Group> _groups = new List<Group>();
                    using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                    {
                        con.Open();
                        using (MySqlCommand cmd = new MySqlCommand())
                        {
                            cmd.Connection = con;
                            cmd.CommandText = "SELECT GroupID FROM groups_gifts WHERE GiftID = @gid;";
                            cmd.Parameters.AddWithValue("@gid", ID);
                            cmd.Prepare();
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    _groups.Add(new Group(Convert.ToUInt64(reader["GroupID"])));
                                }
                                return _groups;
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// Fetch an existing gift
            /// </summary>
            /// <param name="id">The existing gift</param>
            public Gift(ulong id)
            {
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT * FROM gifts WHERE GiftID = @id;";
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                ID = id;
                                User = new User(Convert.ToUInt64(reader["UserID"]));
                                Name = Convert.ToString(reader["GiftName"]);
                                Description = Convert.ToString(reader["GiftDescription"]);
                                Url = Convert.ToString(reader["GiftURL"]);
                                Cost = Convert.ToDouble(reader["GiftCost"]);
                                Stores = Convert.ToString(reader["GiftStores"]);
                                Quantity = Convert.ToUInt32(reader["GiftQuantity"]);
                                Color = Convert.ToString(reader["GiftColor"]);
                                ColorText = Convert.ToString(reader["GiftColorText"]);
                                Size = Convert.ToString(reader["GiftSize"]);
                                Category = new Category(Convert.ToUInt64(reader["CategoryID"]));
                                Rating = Convert.ToDouble(reader["GiftRating"]);
                                TimeStamp = (DateTime)(reader["GiftAddStamp"]);
                                if (DBNull.Value == reader["GiftReceivedDate"])
                                {
                                    DateReceived = null;
                                }
                                else
                                {
                                    DateReceived = (DateTime)(reader["GiftReceivedDate"]);
                                }
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// Create a new gift with the given name
            /// </summary>
            /// <param name="Name">The new gifts name</param>
            public Gift(string Name)
            {
                this.Name = Name;
            }
            /// <summary>
            /// Create the gift within the database
            /// </summary>
            public void Create()
            {
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "INSERT INTO gifts (UserID, GiftName, GiftDescription, GiftURL, GiftCost, GiftStores, GiftQuantity, GiftColor, GiftColorText, GiftSize, CategoryID, GiftRating, GiftReceivedDate) "
                                        + "VALUES (@uid, @name, @desc, @url, @cost, @stores, @quantity, @hex, @color, @size, @category, @rating, @rec);";
                        cmd.Parameters.AddWithValue("@uid", User.ID);
                        cmd.Parameters.AddWithValue("@name", Name);
                        cmd.Parameters.AddWithValue("@desc", Description);
                        cmd.Parameters.AddWithValue("@url", Url);
                        cmd.Parameters.AddWithValue("@cost", Cost);
                        cmd.Parameters.AddWithValue("@stores", Stores);
                        cmd.Parameters.AddWithValue("@quantity", Quantity);
                        cmd.Parameters.AddWithValue("@hex", Color);
                        cmd.Parameters.AddWithValue("@color", ColorText);
                        cmd.Parameters.AddWithValue("@size", Size);
                        cmd.Parameters.AddWithValue("@category", Category.CategoryId);
                        cmd.Parameters.AddWithValue("@rating", Rating);
                        cmd.Parameters.AddWithValue("@rec", DateReceived.HasValue ? "" : DateReceived.Value.ToString("yyyy-MM-dd"));
                        cmd.Prepare();
                        if (cmd.ExecuteNonQuery() == 1)
                        {
                            ID = Convert.ToUInt64(cmd.LastInsertedId);
                            using (MySqlCommand timer = new MySqlCommand())
                            {
                                timer.Connection = con;
                                timer.CommandText = "SELECT GiftAddStamp FROM gifts WHERE GiftID = @gid;";
                                timer.Parameters.AddWithValue("@gid", ID);
                                timer.Prepare();
                                using (MySqlDataReader reader = timer.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        TimeStamp = (DateTime)(reader["GiftAddStamp"]);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// Update this gift in the database
            /// </summary>
            public void Update()
            {
                if (ID == 0)
                {
                    Create();
                }
                else
                {
                    using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                    {
                        con.Open();
                        using (MySqlCommand cmd = new MySqlCommand())
                        {
                            cmd.Connection = con;
                            cmd.CommandText = "UPDATE gifts " +
                                                "SET GiftName = @name, " +
                                                "GiftDescription = @description, " +
                                                "GiftURL = @url, " +
                                                "GiftCost = @cost, " +
                                                "GiftStores = @stores, " +
                                                "GiftQuantity = @quant, " +
                                                "GiftColor = @color, " +
                                                "GiftColorText = @colorText, " +
                                                "GiftSize = @size, " +
                                                "CategoryID = @cid, " +
                                                "GiftRating = @rating, " +
                                                "GiftReceivedDate = @rec " +
                                                "WHERE GiftID = @gid;";
                            cmd.Parameters.AddWithValue("@name", Name);
                            cmd.Parameters.AddWithValue("@description", Description);
                            cmd.Parameters.AddWithValue("@url", Url);
                            cmd.Parameters.AddWithValue("@cost", Cost);
                            cmd.Parameters.AddWithValue("@stores", Stores);
                            cmd.Parameters.AddWithValue("@quant", Quantity);
                            cmd.Parameters.AddWithValue("@color", Color);
                            cmd.Parameters.AddWithValue("@colorText", ColorText);
                            cmd.Parameters.AddWithValue("@size", Size);
                            cmd.Parameters.AddWithValue("@cid", Category.CategoryId);
                            cmd.Parameters.AddWithValue("@rating", Rating);
                            cmd.Parameters.AddWithValue("@gid", ID);
                            cmd.Parameters.AddWithValue("@rec", DateReceived.HasValue ? null : DateReceived.Value.ToString("yyyy-MM-dd"));
                            cmd.Prepare();
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            /// <summary>
            /// Delete this gift from the database
            /// </summary>
            public void Delete()
            {
                if (ID != 0)
                {
                    // Delete from reservations, remove from groups, remove our image, delete from main table:
                    foreach (Reservation res in Reservations)
                    {
                        res.Delete();
                    }
                    foreach (Group group in Groups)
                    {
                        group.Remove(this);
                    }
                    RemoveImage();
                    using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                    {
                        con.Open();
                        using (MySqlCommand cmd = new MySqlCommand())
                        {
                            cmd.Connection = con;
                            cmd.CommandText = "DELETE FROM gifts WHERE GiftID = @gid;";
                            cmd.Parameters.AddWithValue("@gid", ID);
                            cmd.Prepare();
                            cmd.ExecuteNonQuery();
                            ID = 0;
                        }
                    }
                }
            }
            /// <summary>
            /// Save the specified image as the image for this gift.
            /// </summary>
            /// <param name="contents">The image as a byte array</param>
            public void SaveImage(byte[] contents)
            {
                ImageProcessor processor = new ImageProcessor(contents);
                File.WriteAllBytes(Directory.GetCurrentDirectory() + "/resources/images/gifts/Gift" + ID + Constants.ImageFormat, processor.Data);
            }
            /// <summary>
            /// Remove the associated image
            /// </summary>
            public void RemoveImage()
            {
                File.Delete(Directory.GetCurrentDirectory() + "/resources/images/gifts/Gift" + ID + Constants.ImageFormat);
            }
            /// <summary>
            /// Get the image associated with this gift
            /// </summary>
            /// <returns>A qualified path for this gift's image</returns>
            /// <remarks>
            /// Note that qualified means with respect to the server's root, *not* necessarily '/' or 'C:\'
            /// </remarks>
            public string GetImage()
            {
                return GetImage(ID);
            }
            /// <summary>
            /// Get the image for a specified giftID
            /// </summary>
            /// <param name="id">The specified GiftID</param>
            /// <returns>The qualified path (See GetImage() for more information)</returns>
            public static string GetImage(ulong id)
            {
                string path = Directory.GetCurrentDirectory() + "/resources/images/gifts/Gift" + id + Constants.ImageFormat;
                // if file exists, return path. Otherwise, return default
                // Race condition, but I don't know how to solve (yet)
                if (File.Exists(path))
                {
                    return "/resources/images/gifts/Gift" + id + Constants.ImageFormat;
                }
                else
                {
                    return "resources/images/gifts/default" + Constants.ImageFormat;
                }
            }
            /// <summary>
            /// Add this gift to a group
            /// </summary>
            /// <param name="group">The group that can now view this gift</param>
            public void Add(Group group)
            {
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "INSERT INTO groups_gifts (GroupID, GiftID) VALUES (@groupId, @giftId);";
                        cmd.Parameters.AddWithValue("@groupId", group.ID);
                        cmd.Parameters.AddWithValue("@giftId", ID);
                        cmd.Prepare();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            /// <summary>
            /// Remove this gift from a group
            /// </summary>
            /// <param name="group">The group that will no longer be able to view this gift</param>
            public void Remove(Group group)
            {
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "DELETE FROM groups_gifts WHERE GiftID = @giftId AND GroupID = @groupId;";
                        cmd.Parameters.AddWithValue("@giftId", ID);
                        cmd.Parameters.AddWithValue("@groupId", group.ID);
                        cmd.Prepare();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            /// <summary>
            /// See if this object is this gift
            /// </summary>
            /// <param name="obj">The object to inspect</param>
            /// <returns>If the object is the same as this gift</returns>
            public override bool Equals(object obj)
            {
                if (obj != null && obj is Gift g)
                {
                    return Equals(g);
                }
                else
                {
                    return false;
                }
            }
            /// <summary>
            /// See if the two gifts are equal
            /// </summary>
            /// <param name="gift">The gift to compare</param>
            /// <returns>If the two gifts are equal</returns>
            public bool Equals(Gift gift)
            {
                return gift != null && gift.ID == ID;
            }
            /// <summary>
            /// Get the hash code for this gift
            /// </summary>
            /// <returns>This gift's hash code</returns>
            public override int GetHashCode()
            {
                return ID.GetHashCode();
            }
            /// <summary>
            /// Serialize this gift as an XML Document
            /// </summary>
            /// <remarks>
            /// This is a serialization for a gift. Note that the User element is _not_ expanded.
            /// This XML Document contains the following fields:
            ///     - giftId: This gift's ID
            ///     - user: The UserID associated with this gift
            ///     - name: This gift's name
            ///     - description: This gift's description
            ///     - url: This gift's URL
            ///     - cost: This gift's cost, encoded WITHOUT the currency. Note that currency isn't currently supported, but will be soon.
            ///     - stores: The stores where this gift is sold, as a string
            ///     - quantity: The quantity desired for this gift
            ///     - color: The hex code for this gift's color, with the leading # included
            ///     - colorText: The color description for this gift's color
            ///     - size: The size of this gift
            ///     - category: The name of the gift category
            ///     - rating: The rating for this gift
            ///     - image: The qualified path for this gift's image
            ///     - groups: The groups that can view this gift
            ///         - Note that each child element of _groups_ is a _group_ element
            ///     - reservations: The reservations currently held for this gift
            ///         - Note that each child element of _reservations_ is a _reservation_ element
            ///         
            /// All this is wrapped in a _gift_ container (I know, pun)
            /// </remarks>
            /// <returns>An XML document with all gift information</returns>
            public XmlDocument Fetch()
            {
                XmlDocument info = new XmlDocument();
                XmlElement container = info.CreateElement("gift");
                info.AppendChild(container);
                XmlElement id = info.CreateElement("giftId");
                id.InnerText = ID.ToString();
                XmlElement user = info.CreateElement("user");
                user.InnerText = User.ID.ToString();
                XmlElement name = info.CreateElement("name");
                name.InnerText = Name;
                XmlElement description = info.CreateElement("description");
                description.InnerText = Description;
                XmlElement url = info.CreateElement("url");
                url.InnerText = Url;
                XmlElement cost = info.CreateElement("cost");
                cost.InnerText = Cost.ToString("#.##");
                XmlElement stores = info.CreateElement("stores");
                stores.InnerText = Stores;
                XmlElement quantity = info.CreateElement("quantity");
                quantity.InnerText = Quantity.ToString();
                XmlElement color = info.CreateElement("color");
                color.InnerText = "#" + Color;
                XmlElement colorText = info.CreateElement("colorText");
                colorText.InnerText = ColorText;
                XmlElement size = info.CreateElement("size");
                size.InnerText = Size;
                XmlElement category = info.CreateElement("category");
                category.InnerText = Category.Name;
                XmlElement rating = info.CreateElement("rating");
                rating.InnerText = Rating.ToString();
                XmlElement image = info.CreateElement("image");
                image.InnerText = GetImage();
                XmlElement groups = info.CreateElement("groups");
                foreach (Group group in Groups)
                {
                    XmlElement groupElem = info.CreateElement("group");
                    groupElem.InnerText = group.ID.ToString();
                    groups.AppendChild(groupElem);
                }
                XmlElement reservations = info.CreateElement("reservations");
                foreach (Reservation reservation in Reservations)
                {
                    reservations.AppendChild(info.ImportNode(reservation.Fetch().DocumentElement, true));
                }

                container.AppendChild(id);
                container.AppendChild(user);
                container.AppendChild(name);
                container.AppendChild(description);
                container.AppendChild(url);
                container.AppendChild(cost);
                container.AppendChild(stores);
                container.AppendChild(quantity);
                container.AppendChild(color);
                container.AppendChild(colorText);
                container.AppendChild(size);
                container.AppendChild(category);
                container.AppendChild(rating);
                container.AppendChild(image);
                container.AppendChild(groups);
                container.AppendChild(reservations);

                return info;
            }

            /// <summary>
            /// Fetch a gift, as visible by the viewer
            /// </summary>
            /// <param name="viewer">The viewer</param>
            /// <returns>All information about this gift the viewer can see</returns>
            public XmlDocument Fetch(User viewer)
            {
                // First make sure gifts are in common
                if (User.GetGifts(viewer).Exists(g => g.ID == ID))
                {
                    XmlDocument info = new XmlDocument();
                    XmlElement container = info.CreateElement("gift");
                    info.AppendChild(container);
                    XmlElement id = info.CreateElement("giftId");
                    id.InnerText = ID.ToString();
                    XmlElement user = info.CreateElement("user");
                    user.InnerText = User.ID.ToString();
                    XmlElement name = info.CreateElement("name");
                    name.InnerText = Name;
                    XmlElement description = info.CreateElement("description");
                    description.InnerText = Description;
                    XmlElement url = info.CreateElement("url");
                    url.InnerText = Url;
                    XmlElement cost = info.CreateElement("cost");
                    cost.InnerText = Cost.ToString("#.##");
                    XmlElement stores = info.CreateElement("stores");
                    stores.InnerText = Stores;
                    XmlElement quantity = info.CreateElement("quantity");
                    quantity.InnerText = Quantity.ToString();
                    XmlElement color = info.CreateElement("color");
                    color.InnerText = "#" + Color;
                    XmlElement colorText = info.CreateElement("colorText");
                    colorText.InnerText = ColorText;
                    XmlElement size = info.CreateElement("size");
                    size.InnerText = Size;
                    XmlElement category = info.CreateElement("category");
                    category.InnerText = Category.Name;
                    XmlElement rating = info.CreateElement("rating");
                    rating.InnerText = Rating.ToString();
                    XmlElement image = info.CreateElement("image");
                    image.InnerText = GetImage();
                    XmlElement groups = info.CreateElement("groups");
                    // only attach group if viewer is also in that group
                    foreach (Group group in Groups.FindAll(group => group.Users.Exists(u => u.ID == ID)))
                    {
                        XmlElement groupElem = info.CreateElement("group");
                        groupElem.InnerText = group.ID.ToString();
                        groups.AppendChild(groupElem);
                    }
                    XmlElement reservations = info.CreateElement("reservations");
                    foreach (Reservation reservation in Reservations)
                    {
                        reservations.AppendChild(info.ImportNode(reservation.Fetch(viewer).DocumentElement, true));
                    }

                    container.AppendChild(id);
                    container.AppendChild(user);
                    container.AppendChild(name);
                    container.AppendChild(description);
                    container.AppendChild(url);
                    container.AppendChild(cost);
                    container.AppendChild(stores);
                    container.AppendChild(quantity);
                    container.AppendChild(color);
                    container.AppendChild(colorText);
                    container.AppendChild(size);
                    container.AppendChild(category);
                    container.AppendChild(rating);
                    container.AppendChild(image);
                    container.AppendChild(groups);
                    container.AppendChild(reservations);

                    return info;
                }
                else
                {
                    XmlDocument info = new XmlDocument();
                    XmlElement container = info.CreateElement("gift");
                    info.AppendChild(container);

                    return info;
                }
            }
        }
    }
}