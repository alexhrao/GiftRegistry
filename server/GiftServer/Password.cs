﻿using GiftServer.Data;
using GiftServer.Exceptions;
using System;
using System.Security.Cryptography;
using System.Web;
using System.Xml;

namespace GiftServer
{
    namespace Security
    {
        public sealed class Password : IFetchable
        {
            /// <summary>
            /// Size of salt
            /// </summary>
            private const int SaltSize = 16;

            /// <summary>
            /// Size of hash
            /// </summary>
            private const int HashSize = 20;
            private readonly byte[] _hash;
            private readonly byte[] _salt;
            public string Hash
            {
                get
                {
                    return Convert.ToBase64String(_hash);
                }
            }
            public string Salt
            {
                get
                {
                    return Convert.ToBase64String(_salt);
                }
            }
            public readonly int Iterations;

            public Password(string hash, string salt, int iterations)
            {
                _hash = Convert.FromBase64String(hash);
                _salt = Convert.FromBase64String(salt);
                Iterations = iterations;
            }

            public Password(string password) : this(password, 10000) { }
            public Password(string password, int iterations)
            {
                this.Iterations = iterations;
                if (password == null || password.Length <= 3)
                {
                    throw new InvalidPasswordException();
                }
                _salt = new byte[SaltSize];
                using (RNGCryptoServiceProvider crypt = new RNGCryptoServiceProvider())
                {
                    crypt.GetBytes(_salt);
                }
                _hash = new Rfc2898DeriveBytes(password, _salt, iterations).GetBytes(HashSize);
            }

            public bool Verify(string password)
            {
                byte[] hash = new Rfc2898DeriveBytes(password, _salt, Iterations).GetBytes(HashSize);
                for (int i = 0; i < HashSize; i++)
                {
                    if (hash[i] != _hash[i])
                    {
                        return false;
                    }
                }
                return true;
            }

            public XmlDocument Fetch()
            {
                XmlDocument info = new XmlDocument();
                XmlElement container = info.CreateElement("password");
                info.AppendChild(container);

                XmlElement hash = info.CreateElement("hash");
                hash.InnerText = HttpUtility.HtmlEncode(Hash);
                XmlElement salt = info.CreateElement("salt");
                salt.InnerText = HttpUtility.HtmlEncode(Salt);
                XmlElement iterations = info.CreateElement("iterations");
                iterations.InnerText = HttpUtility.HtmlEncode(Iterations);

                container.AppendChild(hash);
                container.AppendChild(salt);
                container.AppendChild(iterations);

                return info;
            }
        }
    }
}