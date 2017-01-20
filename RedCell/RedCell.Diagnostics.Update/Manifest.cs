﻿using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace RedCell.Diagnostics.Update
{
    internal class Manifest
    {
        #region Fields
        private string _data;
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes a new instance of the <see cref="Manifest"/> class.
        /// </summary>
        /// <param name="data">The data.</param>
        public Manifest(string data)
        {
            Load(data);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the version.
        /// </summary>
        /// <value>The version.</value>
        public int Version { get; private set; }

        /// <summary>
        /// Gets the check interval.
        /// </summary>
        /// <value>The check interval.</value>
        public int CheckInterval { get; private set; }

        /// <summary>
        /// Gets the remote configuration URI.
        /// </summary>
        /// <value>The remote configuration URI.</value>
        public string RemoteConfigUri { get; private set; }

        /// <summary>
        /// Gets the security token.
        /// </summary>
        /// <value>The security token.</value>
        public string SecurityToken { get; private set; }

        /// <summary>
        /// Gets the base URI.
        /// </summary>
        /// <value>The base URI.</value>
        public string BaseUri { get; private set; }

        /// <summary>
        /// Gets the payload.
        /// </summary>
        /// <value>The payload.</value>
        public string[] Payloads { get; private set; }
        #endregion

        #region Methods
        /// <summary>
        /// Loads the specified data.
        /// </summary>
        /// <param name="data">The data.</param>
        private void Load (string data)
        {
            _data = data;
            try
            {
                // Load config from XML
                var xml = XDocument.Parse(data);
                if (xml.Root.Name.LocalName != "Manifest")
                {
                    Log.Write("Root XML element '{0}' is not recognized, stopping.", xml.Root.Name);
                    return;
                }

                // Set properties.
                Version = int.Parse(xml.Root.Attribute("version").Value);
                CheckInterval = int.Parse(xml.Root.Element("CheckInterval").Value);
                SecurityToken = xml.Root.Element("SecurityToken").Value;
                RemoteConfigUri = xml.Root.Element("RemoteConfigUri").Value;
                BaseUri = xml.Root.Element("BaseUri").Value;
                Payloads = xml.Root.Elements("Payload").Select(x => x.Value).ToArray();
            }
            catch (Exception ex)
            {
                Log.Write("Error: {0}", ex.Message);
                return;
            }
        }

        /// <summary>
        /// Writes the specified path.
        /// </summary>
        /// <param name="path">The path.</param>
        public void Write (string path)
        {
            File.WriteAllText(path, _data);
        }
        #endregion
    }
}
