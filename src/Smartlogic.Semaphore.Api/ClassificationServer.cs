using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace Smartlogic.Semaphore.Api
{
    /// <summary>
    ///     Classification Server proxy
    /// </summary>
    public class ClassificationServer : LoggingProxyBase
    {
        private const int WEBSERVICE_TIMEOUT_DEFAULT = 120;
        private readonly string _apiKey;
        private readonly Uri _serverUrl;
        private readonly int _timeout;

        /// <summary>
        ///     A proxy class used for communicating with an instance of Semaphore Classification Server
        /// </summary>
        /// <param name="webServiceTimeout">An integer representing the number of seconds to wait before timing out</param>
        /// <param name="serverUrl">
        ///     An absolute <see cref="Uri" /> indicating the Classification Server endpoint
        /// </param>
        /// <exception cref="ArgumentException"></exception>
        public ClassificationServer(int webServiceTimeout, Uri serverUrl) : this(webServiceTimeout, serverUrl, new DefaultTraceLogger())
        {
        }

        /// <summary>
        ///     A proxy class used for communicating with an instance of Semaphore Classification Server
        /// </summary>
        /// <param name="serverUrl">
        ///     An absolute <see cref="Uri" /> indicating the Classification Server endpoint
        /// </param>
        /// <exception cref="ArgumentException"></exception>
        public ClassificationServer(Uri serverUrl) : this(WEBSERVICE_TIMEOUT_DEFAULT, serverUrl, new DefaultTraceLogger())
        {
        }

        /// <summary>
        ///     A proxy class used for communicating with an instance of Semaphore Classification Server
        /// </summary>
        /// <param name="webServiceTimeout">An integer representing the number of seconds to wait before timing out</param>
        /// <param name="serverUrl">
        ///     An absolute <see cref="Uri" /> indicating the Classification Server endpoint
        /// </param>
        /// <param name="logger"></param>
        /// <exception cref="ArgumentException"></exception>
        public ClassificationServer(int webServiceTimeout, Uri serverUrl, ILogger logger)
            : base(logger)
        {
            if (serverUrl == null) throw new ArgumentException("Missing server Url", nameof(serverUrl));
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (webServiceTimeout <= 0) throw new ArgumentException("Web service timeout must be greater than 0", nameof(webServiceTimeout));
            if (webServiceTimeout > int.MaxValue/1000)
                throw new ArgumentException("Web service timeout must be less than " + short.MaxValue/1000, nameof(webServiceTimeout));
            if (!serverUrl.IsAbsoluteUri) throw new ArgumentException("Server Url must be an absolute Uri", nameof(serverUrl));

            _serverUrl = serverUrl;
            _timeout = webServiceTimeout;
            BoundaryString = Guid.NewGuid().ToString().Replace("-", "");
        }

        /// <summary>
        ///     Constructs a SearchEnhancement class for use with communicating with a particular instance of Search Enhancement
        ///     Server
        /// </summary>
        /// <param name="webServiceTimeout">An integer representing the number of seconds to wait before timing out</param>
        /// <param name="serverUrl">
        ///     An absolute <see cref="Uri" /> indicating the Search Enhancement Server endpoint
        /// </param>
        /// <param name="logger"></param>
        /// <param name="apiKey">An optional apikey string used for connecting to OAuth 2.0 secured services</param>
        /// <exception cref="System.ArgumentException">Missing server Url;serverUrl</exception>
        /// <exception cref="ArgumentException"></exception>
        public ClassificationServer(int webServiceTimeout, Uri serverUrl, ILogger logger, string apiKey = "") : this(webServiceTimeout, serverUrl, logger)
        {
            if (!string.IsNullOrEmpty(apiKey))
            {
                var regex = new Regex("^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=|[A-Za-z0-9+/]{4})$");
                if (!regex.IsMatch(apiKey))
                {
                    throw new ArgumentException("apikey is invalid", nameof(apiKey));
                }
            }
            _apiKey = apiKey;
        }

        /// <summary>
        ///     The BoundaryString used for delimiting multi-part form messages sent to Classification Server. A default value is
        ///     generated automatically although this property allows the default to be overriden if a specific value is required.
        /// </summary>
        public string BoundaryString { get; set; }

        /// <summary>
        ///     Passes values and binary content to Classification Server for processing
        /// </summary>
        /// <param name="title">The title of the item being classified. This value is optional.</param>
        /// <param name="document">A byte array representing binary content to be classified</param>
        /// <param name="fileName">Where binary content is being classified, a filename is required</param>
        /// <param name="metaValues">A dictionary of values to be classified</param>
        /// <param name="altBody">
        ///     When classifying HTML content, <paramref name="altBody" /> is used to pass the body of the page
        /// </param>
        /// <param name="options">Additional classification options. If no values are passed, server defaults are used</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public ClassificationResult Classify(string title,
            byte[] document,
            string fileName,
            Dictionary<string, string> metaValues,
            string altBody,
            ClassificationOptions options)
        {
            if (metaValues == null) throw new ArgumentException("Missing metaValues", nameof(metaValues));
            if (document != null && string.IsNullOrEmpty(fileName)) throw new ArgumentException("Documents must have an associated filename", nameof(fileName));
            if (document == null && !string.IsNullOrEmpty(fileName))
                throw new ArgumentException("Filename parameter should not be set unless an associated document is passed via the document parameter",
                    nameof(document));

            WriteLow("Calling Tagging API (Binary). Item Title={0}, FileName={1}", title, fileName);

            var oFrmFields =
                new Dictionary<string, string>
                {
                    {"operation", "CLASSIFY"},
                    {"title", title},
                    {"body", altBody}
                };

            if (options != null)
            {
                if (options.Threshold.HasValue)
                {
                    oFrmFields.Add("threshold", options.Threshold.Value.ToString(CultureInfo.InvariantCulture));
                }
                if (!string.IsNullOrEmpty(options.Type))
                {
                    oFrmFields.Add("type", options.Type);
                }
                if (!string.IsNullOrEmpty(options.ClusteringType))
                {
                    oFrmFields.Add("clustering_type", options.ClusteringType);
                }

                if (options.ClusteringThreshold.HasValue)
                {
                    oFrmFields.Add("clustering_threshold", options.ClusteringThreshold.Value.ToString(CultureInfo.InvariantCulture));
                }

                switch (options.ArticleType)
                {
                    case ArticleType.Default:
                    case ArticleType.ServerDefault:
                        break;
                    case ArticleType.SingleArticle:
                        oFrmFields.Add("singlearticle", "1");
                        break;
                    case ArticleType.MultiArticle:
                        oFrmFields.Add("multiarticle", "1");
                        break;
                }
            }

            foreach (var sKey in metaValues.Keys)
                oFrmFields.Add("meta_" + sKey, metaValues[sKey]);

            List<FileUpload> oFiles = null;

            if (!string.IsNullOrEmpty(fileName) && document != null)
            {
                oFiles = new List<FileUpload>
                {
                    new FileUpload
                    {
                        FieldName = "UploadFile",
                        FileName = fileName,
                        Contents = document
                    }
                };
            }
            return PerformWebClassify(oFrmFields, oFiles);
        }


        /// <summary>
        ///     Retrieves a list of the Classification Classes available on the current Classification Server instance
        /// </summary>
        /// <returns></returns>
        /// <exception cref="SemaphoreConnectionException"></exception>
        public Collection<string> GetClassificationClasses()
        {
            var csClasses = new Collection<string>();
            var request = AuthenticatedRequestBuilder.Instance.Build(new Uri(_serverUrl + "?operation=LISTRULENETCLASSES"), _apiKey, Logger);
            WriteLow("CS Request: " + request.RequestUri, null);
            request.Method = "GET";
            request.Timeout = _timeout*1000;

            try
            {
                var oStop = new Stopwatch();
                oStop.Start();
                var oResponse = (HttpWebResponse) request.GetResponse();
                oStop.Stop();

                WriteLow("Response received from CS. Time elapsed {0}:{1}.{2}",
                    oStop.Elapsed.Minutes,
                    oStop.Elapsed.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0').PadLeft(2, '0'),
                    oStop.Elapsed.Milliseconds);

                var stream = oResponse.GetResponseStream();
                if (stream != null)
                    using (var oReader = new StreamReader(stream))
                    {
                        var xDoc = new XmlDocument();
                        xDoc.LoadXml(oReader.ReadToEnd());
                        var nodeList = xDoc.SelectNodes("//Class");

                        if (nodeList == null || nodeList.Count == 0)
                        {
                            throw new SemaphoreConnectionException($"No Classes retrieved from {request.RequestUri.AbsolutePath}");
                        }

                        foreach (XmlNode node in nodeList)
                        {
                            if (node.Attributes != null)
                            {
                                var attrib = node.Attributes["Name"].Value;
                                if (!attrib.StartsWith("TEXTMINE_"))
                                    csClasses.Add(attrib);
                            }
                        }
                    }
                oResponse.Close();
            }
            catch (Exception ex)
            {
                WriteException(ex);
                throw;
            }


            return csClasses;
        }

        /// <summary>
        ///     Returns a list of languages currently configured on Classification Server
        /// </summary>
        /// <returns></returns>
        /// <exception cref="SemaphoreConnectionException"></exception>
        public Collection<ClassificationLanguage> GetLanguages()
        {
            var languages = new Collection<ClassificationLanguage>();
            var request = AuthenticatedRequestBuilder.Instance.Build(new Uri(_serverUrl + "?operation=listlanguages"), _apiKey, Logger);
            WriteLow("CS Request: " + request.RequestUri, null);

            request.Method = "GET";
            request.Timeout = _timeout*1000;

            try
            {
                var oStop = new Stopwatch();
                oStop.Start();
                var oResponse = (HttpWebResponse) request.GetResponse();
                oStop.Stop();

                WriteLow(
                    $"Response received from CS. Time elapsed {oStop.Elapsed.Minutes}:{oStop.Elapsed.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0').PadLeft(2, '0')}.{oStop.Elapsed.Milliseconds}",
                    null);

                var stream = oResponse.GetResponseStream();
                if (stream != null)
                    using (var oReader = new StreamReader(stream))
                    {
                        var xDoc = new XmlDocument();
                        xDoc.LoadXml(oReader.ReadToEnd());
                        var top = xDoc.SelectSingleNode("//languages");
                        if (top?.Attributes != null)
                        {
                            var type = top.Attributes["type"].Value;
                            var nodeList = xDoc.SelectNodes("//language");

                            if (nodeList == null || nodeList.Count == 0)
                            {
                                WriteMedium(
                                    "No languages retrieved from {0}",
                                    request.RequestUri.AbsolutePath);
                                return new Collection<ClassificationLanguage>();
                            }

                            foreach (XmlNode node in nodeList)
                            {
                                if (node.Attributes != null)
                                {
                                    try
                                    {
                                        var id = node.Attributes["id"].Value;
                                        var hasRulesDefined = node.Attributes["has_rules_defined"] != null &&
                                                              node.Attributes["has_rules_defined"].Value == "true";
                                        var isDefault = node.Attributes["default"].Value == "true";
                                        var display = node.Attributes["display"].Value;
                                        var name = node.Attributes["name"].Value;
                                        var newLang = new ClassificationLanguage
                                        {
                                            Id = id,
                                            HasRulesDefined = hasRulesDefined,
                                            IsDefault = isDefault,
                                            DisplayName = display,
                                            Name = name,
                                            Type = type
                                        };
                                        languages.Add(newLang);
                                    }
                                    catch (Exception ex)
                                    {
                                        WriteException(ex);
                                    }
                                }
                            }
                        }
                    }
                oResponse.Close();
            }
            catch (Exception ex)
            {
                WriteException(ex);
                throw;
            }


            return languages;
        }

        /// <summary>
        ///     Retrieves a list of the text mining Classes available on the current Classification Server instance
        /// </summary>
        /// <returns></returns>
        /// <exception cref="SemaphoreConnectionException"></exception>
        public Collection<string> GetTextMiningClasses()
        {
            var csClasses = new Collection<string>();
            var request = AuthenticatedRequestBuilder.Instance.Build(new Uri(_serverUrl + "?operation=LISTRULENETCLASSES"), _apiKey, Logger);
            WriteLow("CS Request: " + request.RequestUri, null);
            request.Method = "GET";
            request.Timeout = _timeout*1000;

            try
            {
                var oStop = new Stopwatch();
                oStop.Start();
                var oResponse = (HttpWebResponse) request.GetResponse();
                oStop.Stop();

                WriteLow("Response received from CS. Time elapsed {0}:{1}.{2}",
                    oStop.Elapsed.Minutes,
                    oStop.Elapsed.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0').PadLeft(2, '0'),
                    oStop.Elapsed.Milliseconds);

                var stream = oResponse.GetResponseStream();
                if (stream != null)
                    using (var oReader = new StreamReader(stream))
                    {
                        var xDoc = new XmlDocument();
                        xDoc.LoadXml(oReader.ReadToEnd());
                        var nodeList = xDoc.SelectNodes("//Class");

                        if (nodeList == null || nodeList.Count == 0)
                        {
                            throw new SemaphoreConnectionException($"No Classes retrieved from {request.RequestUri.AbsolutePath}");
                        }

                        foreach (XmlNode node in nodeList)
                        {
                            if (node.Attributes != null)
                            {
                                var attrib = node.Attributes["Name"].Value;
                                //if (attrib.StartsWith("TEXTMINE_"))
                                csClasses.Add(attrib);
                            }
                        }
                    }
                oResponse.Close();
            }
            catch (Exception ex)
            {
                WriteException(ex);
                throw;
            }


            return csClasses;
        }

        /// <summary>
        ///     Returns the version of Classification Server
        /// </summary>
        /// <returns></returns>
        /// <exception cref="SemaphoreConnectionException"></exception>
        public string GetVersion()
        {
            var result = "0.0.0.0";
            var request = AuthenticatedRequestBuilder.Instance.Build(new Uri(_serverUrl + "?operation=version"), _apiKey, Logger);
            WriteLow("CS Request: " + request.RequestUri, null);
            request.Method = "GET";
            request.Timeout = _timeout*1000;

            try
            {
                var oStop = new Stopwatch();
                oStop.Start();
                var oResponse = (HttpWebResponse) request.GetResponse();
                oStop.Stop();

                WriteLow("Response received from CS. Time elapsed {0}:{1}.{2}",
                    oStop.Elapsed.Minutes,
                    oStop.Elapsed.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0').PadLeft(2, '0'),
                    oStop.Elapsed.Milliseconds);

                var stream = oResponse.GetResponseStream();
                if (stream != null)
                    using (var oReader = new StreamReader(stream))
                    {
                        var xDoc = new XmlDocument();
                        xDoc.LoadXml(oReader.ReadToEnd());
                        var top = xDoc.SelectSingleNode("//version");
                        if (top == null) return result;
                        var versionString = top.InnerText;
                        versionString = versionString.Replace("Semaphore ", string.Empty);
                        var endOfVersionIndex = versionString.IndexOf(" - Classification Server", StringComparison.InvariantCulture);
                        versionString = versionString.Substring(0, endOfVersionIndex);
                        result = versionString;
                    }

                oResponse.Close();
            }
            catch (Exception ex)
            {
                WriteException(ex);
                throw;
            }


            return result;
        }

        /// <summary>
        ///     Passes values and binary content to Classification Server for processing
        /// </summary>
        /// <param name="title">The title of the item being classified. This value is optional.</param>
        /// <param name="document">A byte array representing binary content to be classified</param>
        /// <param name="fileName">Where binary content is being classified, a filename is required</param>
        /// <param name="metaValues">A dictionary of values to be classified</param>
        /// <param name="altBody">
        ///     When classifying HTML content, <paramref name="altBody" /> is used to pass the body of the page
        /// </param>
        /// <param name="options">Additional classification options. If no values are passed, server defaults are used</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public ClassificationResult TextMine(string title,
            byte[] document,
            string fileName,
            Dictionary<string, string> metaValues,
            string altBody,
            ClassificationOptions options)
        {
            if (metaValues == null) throw new ArgumentException("Missing metaValues", nameof(metaValues));
            if (document != null && string.IsNullOrEmpty(fileName)) throw new ArgumentException("Documents must have an associated filename", nameof(fileName));
            if (document == null && !string.IsNullOrEmpty(fileName))
                throw new ArgumentException("Filename parameter should not be set unless an associated document is passed via the document parameter",
                    nameof(document));

            WriteLow(
                $"Calling TextMining API (Binary). Item Title={title}, FileName={fileName}",
                null);

            var oFrmFields =
                new Dictionary<string, string>
                {
                    {"operation", "CLASSIFY"},
                    {"title", title},
                    {"body", altBody},
                    {"operation_mode", "TextMine"}
                };

            if (options != null)
            {
                if (options.Threshold.HasValue)
                {
                    oFrmFields.Add("threshold", options.Threshold.Value.ToString(CultureInfo.InvariantCulture));
                }
                if (!string.IsNullOrEmpty(options.Type))
                {
                    oFrmFields.Add("type", options.Type);
                }
                if (!string.IsNullOrEmpty(options.ClusteringType))
                {
                    oFrmFields.Add("clustering_type", options.ClusteringType);
                }

                if (options.ClusteringThreshold.HasValue)
                {
                    oFrmFields.Add("clustering_threshold", options.ClusteringThreshold.Value.ToString(CultureInfo.InvariantCulture));
                }

                switch (options.ArticleType)
                {
                    case ArticleType.Default:
                        break;
                    case ArticleType.SingleArticle:
                        oFrmFields.Add("singlearticle", string.Empty);
                        break;
                    case ArticleType.MultiArticle:
                        oFrmFields.Add("multiarticle", string.Empty);
                        break;
                }
            }

            foreach (var sKey in metaValues.Keys)
                oFrmFields.Add("meta_" + sKey, metaValues[sKey]);

            List<FileUpload> oFiles = null;

            if (!string.IsNullOrEmpty(fileName) && document != null)
            {
                oFiles = new List<FileUpload>
                {
                    new FileUpload
                    {
                        FieldName = "UploadFile",
                        FileName = fileName,
                        Contents = document
                    }
                };
            }
            return PerformWebClassify(oFrmFields, oFiles);
        }


        [SuppressMessage("Microsoft.Usage", "CA2202",
            Justification = "Using statement handles multiple Dispose appropriately")]
        private ClassificationResult PerformWebClassify(Dictionary<string, string> formFields, IEnumerable<FileUpload> files)
        {
            var sBoundary = BoundaryString;
            const string newLine = "\r\n";

            WriteLow($"Classifying data using server Url={_serverUrl}", null);

            var request = AuthenticatedRequestBuilder.Instance.Build(_serverUrl, _apiKey, Logger);

            request.ContentType = $"multipart/form-data; boundary={sBoundary}";
            request.Method = "POST";
            request.Timeout = _timeout*1000;

            try
            {
                using (var oMs = new MemoryStream())
                {
                    using (var oSw = new StreamWriter(oMs))
                    {
                        #region Upload form fields

                        if (formFields != null)
                        {
                            foreach (var s in formFields.Keys)
                            {
                                oSw.Write("--{0}{1}", sBoundary, newLine);
                                oSw.Write("Content-Disposition: form-data; name=\"{0}\"{1}{1}{2}{1}",
                                    s,
                                    newLine,
                                    formFields[s]);
                                WriteLow("Added form-data: name={0}, value={1}", s, formFields[s]);
                            }
                        }

                        #endregion

                        #region Upload files

                        if (files != null)
                        {
                            foreach (var oFile in files)
                            {
                                oSw.Write("--{0}{1}", sBoundary, newLine);
                                oSw.Write("Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"{2}",
                                    oFile.FieldName,
                                    oFile.FileName,
                                    newLine);
                                oSw.Write("Content-Type: application/octet-stream{0}{0}", newLine);
                                oSw.Flush();

                                oMs.Write(oFile.Contents, 0, oFile.Contents.Length);
                                oSw.Write(newLine);
                            }
                        }

                        #endregion

                        oSw.Write("--{0}--{1}", sBoundary, newLine);
                        oSw.Flush();

                        request.ContentLength = oMs.Length;

                        WriteLow($"Sending {oMs.Length} bytes", null);

                        using (var s = request.GetRequestStream())
                        {
                            oMs.WriteTo(s);
                            s.Close();
                        }
                        oSw.Close();
                    }
                    oMs.Close();
                }

                WriteLow("Requesting the response", null);
                HttpWebResponse oResponse;
                try
                {
                    var oStop = new Stopwatch();
                    oStop.Start();
                    oResponse = (HttpWebResponse) request.GetResponse();
                    oStop.Stop();
                    WriteLow("Response received from CS. Time elapsed M: {0} S: {1} MS: {2}",
                        oStop.Elapsed.Minutes,
                        oStop.Elapsed.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'),
                        oStop.Elapsed.Milliseconds);
                }
                catch (WebException oWeX)
                {
                    //Record the error, however this is an expected response from CS when an error has occured,
                    //but this is not 100% true. So we will check the response property of the exception
                    //to determine if an error has actually occured in CS or whether it is a net exception e.g. timeout, invalid file etc
                    //If the response contains an error string, we will throw this as the exception later on
                    //otherwise we will throw the original exception
                    if (oWeX.Status == WebExceptionStatus.Timeout)
                    {
                        WriteError("Timeout Exception occurred. Timeout setting: {0}", _timeout);
                        throw new TimeoutException($"Call to Classification server timed out after {_timeout} seconds", oWeX);
                    }

                    WriteException(oWeX);
                    //throw;
                    oResponse = (HttpWebResponse) oWeX.Response;
                    if (oResponse == null) throw; //Re-throw the original exception (TimeOut)
                }
                WriteLow(
                    $"API Response Code={oResponse.StatusCode}",
                    null);

                ClassificationResult oParser = null;
                var stream = oResponse.GetResponseStream();

                if (stream != null)
                    using (var oReader = new StreamReader(stream))
                    {
                        oParser = new ClassificationResult(oReader.ReadToEnd());
                    }
                oResponse.Close();
                WriteLow("Closed response", null);
                if (oParser != null)
                {
                    WriteLow("Received response: {0}", oParser.Response.OuterXml);
                }
                return oParser;
            }
            catch (ThreadAbortException oTeX)
            {
                WriteException(oTeX);
                throw;
            }
            catch (WebException oWeX)
            {
                WriteError("Web Exception occurred. Timeout setting: {0}", _timeout);
                WriteException(oWeX);
                throw;
            }
            catch (Exception oX)
            {
                WriteException(oX);
                throw;
            }
        }

        #region Nested type: FileUpload

        /// <summary>
        ///     Holds the information about the file(s) to be uploaded.
        /// </summary>
        internal struct FileUpload
        {
            /// <summary>
            ///     The byte array content to be uploaded.
            /// </summary>
            public byte[] Contents { get; set; }

            /// <summary>
            ///     The HTML form field the file should be uploaded into.
            /// </summary>
            public string FieldName { get; set; }

            /// <summary>
            ///     The name of the file to be uploaded.
            /// </summary>
            public string FileName { get; set; }
        }

        #endregion
    }
}