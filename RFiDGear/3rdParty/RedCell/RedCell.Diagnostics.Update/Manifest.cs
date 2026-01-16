using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Serilog;

namespace RedCell.Diagnostics.Update
{
    internal class Manifest
    {
        #region Fields

        private static readonly ILogger Logger = Serilog.Log.ForContext<Manifest>();
        private string _data;

        #endregion Fields

        #region Initialization

        /// <summary>
        /// Initializes a new instance of the <see cref="Manifest"/> class.
        /// </summary>
        /// <param name="data">The data.</param>
        public Manifest(string data)
        {
            Load(data);
        }

        #endregion Initialization

        #region Properties

        /// <summary>
        /// Gets the version.
        /// </summary>
        /// <value>The version.</value>
        public Version Version { get; set; }

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
        /// Gets the base URI.
        /// </summary>
        /// <value>The base URI.</value>
        public string VersionInfoText { get; private set; }

        /// <summary>
        /// Gets the payload.
        /// </summary>
        /// <value>The payload.</value>
        public string[] Payloads { get; private set; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Loads the specified data.
        /// </summary>
        /// <param name="data">The data.</param>
        private void Load(string data)
        {
            _data = data;

            try
            {
                // Load config from XML
                string txt;

                if (data[0] == 65279) // remove BOM if present
                {
                    txt = new string(data.ToCharArray(1, data.Length - 1));
                }
                else
                {
                    txt = new string(data.ToCharArray());
                }

                var xml = XDocument.Parse(SanitizeXmlForParsing(txt));

                if (xml.Root == null)
                {
                    Logger.Warning("Root XML element is missing, stopping.");
                    return;
                }

                if (xml.Root.Name.LocalName != "Manifest")
                {
                    Logger.Warning("Root XML element '{RootName}' is not recognized, stopping.", xml.Root.Name);
                    return;
                }

                // Set properties.
                var root = xml.Root;
                var ns = root.Name.Namespace;

                if (!Version.TryParse(xml.Root.Attribute("version")?.Value, out var manifestVersion))
                {
                    Logger.Warning("Manifest version could not be parsed, stopping.");
                    return;
                }

                Version = manifestVersion;

                if (!TryReadElementValue(root, ns, "CheckInterval", out var checkIntervalValue)
                    || !int.TryParse(checkIntervalValue, out var checkInterval))
                {
                    Logger.Warning("CheckInterval could not be parsed, stopping.");
                    return;
                }

                if (!TryReadElementValue(root, ns, "SecurityToken", out var securityToken)
                    || !TryReadElementValue(root, ns, "RemoteConfigUri", out var remoteConfigUri)
                    || !TryReadElementValue(root, ns, "BaseUri", out var baseUri)
                    || !TryReadElementValue(root, ns, "VersionInfoText", out var versionInfoText))
                {
                    Logger.Warning("Manifest elements are missing, stopping.");
                    return;
                }

                var payloads = FindElements(root, ns, "Payload").Select(x => x.Value).ToArray();
                if (payloads.Length == 0)
                {
                    Logger.Warning("Manifest payloads are missing, stopping.");
                    return;
                }

                CheckInterval = checkInterval;
                SecurityToken = securityToken;
                RemoteConfigUri = remoteConfigUri;
                BaseUri = baseUri;
                Payloads = payloads;
                VersionInfoText = versionInfoText;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error parsing manifest.");
                return;
            }
        }

        /// <summary>
        /// Sanitizes the XML string by removing invalid XML characters and escaping stray ampersands.
        /// </summary>
        /// <param name="input">The XML string to sanitize.</param>
        /// <returns>The sanitized XML string.</returns>
        private static string SanitizeXmlForParsing(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input ?? string.Empty;
            }

            var normalized = new StringBuilder(input.Length);
            foreach (var character in input)
            {
                if (XmlConvert.IsXmlChar(character))
                {
                    normalized.Append(character);
                }
            }

            var text = normalized.ToString();
            var escaped = new StringBuilder(text.Length);
            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                if (character != '&')
                {
                    escaped.Append(character);
                    continue;
                }

                if (TryReadEntity(text, index, out var endIndex))
                {
                    escaped.Append(text, index, endIndex - index + 1);
                    index = endIndex;
                    continue;
                }

                escaped.Append("&amp;");
            }

            return escaped.ToString();
        }

        private static bool TryReadEntity(string text, int startIndex, out int endIndex)
        {
            endIndex = text.IndexOf(';', startIndex + 1);
            if (endIndex == -1)
            {
                return false;
            }

            var entityBody = text.Substring(startIndex + 1, endIndex - startIndex - 1);
            if (entityBody.Length == 0)
            {
                return false;
            }

            if (entityBody == "amp" || entityBody == "lt" || entityBody == "gt" || entityBody == "quot"
                || entityBody == "apos")
            {
                return true;
            }

            if (entityBody[0] != '#')
            {
                return false;
            }

            var isHex = entityBody.Length > 2 && (entityBody[1] == 'x' || entityBody[1] == 'X');
            var start = isHex ? 2 : 1;
            if (entityBody.Length <= start)
            {
                return false;
            }

            for (var index = start; index < entityBody.Length; index++)
            {
                var character = entityBody[index];
                if (isHex ? !Uri.IsHexDigit(character) : !char.IsDigit(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadElementValue(XElement root, XNamespace ns, string name, out string value)
        {
            var element = FindElement(root, ns, name);
            if (element == null)
            {
                value = string.Empty;
                return false;
            }

            value = element.Value;
            return true;
        }

        private static XElement? FindElement(XElement root, XNamespace ns, string name)
        {
            return root.Element(ns + name) ?? root.Element(name);
        }

        private static IEnumerable<XElement> FindElements(XElement root, XNamespace ns, string name)
        {
            var namespaced = root.Elements(ns + name).ToList();
            return namespaced.Count > 0 ? namespaced : root.Elements(name);
        }

        /// <summary>
        /// Writes the specified path.
        /// </summary>
        /// <param name="path">The path.</param>
        public void Write(string path)
        {
            File.WriteAllText(path, _data);
        }

        #endregion Methods
    }
}
