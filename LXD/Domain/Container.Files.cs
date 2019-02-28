using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Http;
using RestSharp;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace LXD.Domain
{
    public partial class Container : RemoteObject
    {
        public enum FileKind
        {
            Unknown = -1, Nothing = 0, File, Directory, SymLink
        }

        public GetFileResult GetFile(string path)
        {
            IRestRequest request = new RestRequest($"/{Client.Version}/containers/{Name}/files?path={path}", Method.GET, DataFormat.None);
            IRestResponse response = API.Get(request);
            return GetFileResult.Create(response);
        }

        public class GetFileResult
        {
            public int? X_LXD_uid { get; protected set; }
            public int? X_LXD_gid { get; protected set; }
            public int? X_LXD_mode { get; protected set; }
            public FileKind X_LXD_type { get; protected set; }

            public FormatException ThrownHttpHeaderParsingException { get; private set; } = null;
            public bool IsSuccededHttpHeaderParsing => ThrownHttpHeaderParsingException == null;

            protected GetFileResult(int uid, int gid, int mode, FileKind type)
            {
                X_LXD_uid = uid;
                X_LXD_gid = gid;
                X_LXD_mode = mode;
                X_LXD_type = type;
            }

            protected GetFileResult() { }

            protected GetFileResult(string uid, string gid, string mode, string type)
            {
                if (uid != null && int.TryParse(uid, out int o))
                    X_LXD_uid = o;
                else
                    X_LXD_uid = null;
                if (gid != null && int.TryParse(gid, out o))
                    X_LXD_gid = o;
                else
                    X_LXD_gid = null;
                if (mode != null && int.TryParse(mode, out o))
                    X_LXD_mode = o;
                else
                    X_LXD_mode = null;
                X_LXD_type = StringToFileKind(type);
            }

            /// <summary>
            /// Convert string of X-LXD-mode to FileKind.
            /// </summary>
            /// <param name="str">lowercase</param>
            /// <returns></returns>
            private static FileKind StringToFileKind(string str) => str/*.ToLower()*/ switch
            {
                null => FileKind.Nothing,
                "directory" => FileKind.Directory,
                "file" => FileKind.File,
                _ => FileKind.Unknown
            };

            internal static GetFileResult Create(IRestResponse restResponse)
            {
                // X-LXD-uid, X-LXD-gid, X-LXD-mode, X-LXD-type
                string[] tmp = new string[4];

                foreach (var k in restResponse.Headers)
                {
                    if (k.Type != ParameterType.HttpHeader) continue;
                    switch (k.Name.ToLower())
                    {
                        case "x-lxd-uid": tmp[0] = (string)k.Value; break;
                        case "x-lxd-gid": tmp[1] = (string)k.Value; break;
                        case "x-lxd-mode": tmp[2] = (string)k.Value; break;
                        case "x-lxd-type": tmp[3] = ((string)k.Value).ToLower(); break;
                    }
                }

                if (string.IsNullOrEmpty(tmp[3]))
                    return restResponse.ContentType == "application/json" ?
                        new Error(tmp[0], tmp[1], tmp[2], tmp[3], restResponse.Content) : new GetFileResult(tmp[0], tmp[1], tmp[2], tmp[3]);

                switch (StringToFileKind(tmp[3]))
                {
                    case FileKind.Directory:
                        return CreateDirectoryInfo(restResponse, tmp[0], tmp[1], tmp[2], tmp[3]);
                    case FileKind.File:
                        return CreateFileInfo(restResponse, tmp[0], tmp[1], tmp[2], tmp[3]);
                    default:
                        throw new NotSupportedException("Bad X-LXD-type.");
                }
            }

            private static FileInfo CreateFileInfo(IRestResponse restResponse, string uid, string gid, string mode, string type)
                => new FileInfo(uid, gid, mode, type, restResponse.RawBytes);

            private static DirectoryInfo CreateDirectoryInfo(IRestResponse restResponse, string uid, string gid, string mode, string type)
                => new DirectoryInfo(uid, gid, mode, type, restResponse.Content);

            public class FileInfo : GetFileResult
            {
                public byte[] Content { get; private set; }

                internal FileInfo(byte[] content) : base()
                {
                    Content = content;
                }

                internal FileInfo(string uid, string gid, string mode, string type, byte[] content) : base(uid, gid, mode, type)
                {
                    Content = content;
                }
            }

            public class DirectoryInfo : GetFileResult
            {
                public string[] Entries { get; private set; }

                internal DirectoryInfo(string content) : base()
                {
                    Deserialize(JToken.Parse(content));
                }

                internal DirectoryInfo(string uid, string gid, string mode, string type, string content) : base(uid, gid, mode, type)
                {
                    Deserialize(JToken.Parse(content));
                }

                internal void Deserialize(JToken token)
                {
                    JArray metadata = (JArray)token.SelectToken("metadata");
                    if (metadata.Type == JTokenType.Null)
                        Entries = new string[0];
                    else
                        Entries = metadata.Values<string>().ToArray();
                }
            }

            public class Error : GetFileResult
            {
                public string ErrorContent { get; private set; }
                public int? ErrorCode { get; private set; }

                internal Error(string content) : base()
                {
                    Deserialize(JToken.Parse(content));
                }

                internal Error(string uid, string gid, string mode, string type, string content) : base(uid, gid, mode, type)
                {
                    Deserialize(JToken.Parse(content));
                }

                private void Deserialize(JToken tokens)
                {
                    JToken errorToken = tokens.SelectToken("error");
                    if (errorToken != null) ErrorContent = errorToken.Value<string>();
                    JToken errorCodeToken = tokens.SelectToken("error_code");
                    if (errorCodeToken != null)
                        ErrorCode = errorCodeToken.Type == JTokenType.Null ? (int?)null : errorCodeToken.Value<int>();
                }
            }
        }

        private class WebClientEx : WebClient
        {
            public X509CertificateCollection Certificates { get; set; }

            protected override WebRequest GetWebRequest(Uri address)
            {
                HttpWebRequest request = (HttpWebRequest)base.GetWebRequest(address);
                request.ClientCertificates = Certificates;
                return request;
            }
        }

        public StandardResponse PostFile(string path, byte[] content)
        {
            WebClientEx wc = new WebClientEx() { Certificates = API.ClientCertificates };

            var responseByteArray = wc.UploadData(API.BaseUrl + $"{Client.Version}/containers/{Name}/files?path={path}",null, content);
            if (wc.ResponseHeaders[HttpRequestHeader.ContentType] == "application/json")
                return StandardResponse.Parse(System.Text.Encoding.ASCII.GetString(responseByteArray));
            else
                return new StandardResponse.InvalidResponse();
        }

        public StandardResponse PostFile(string path, byte[] content, int? uid, int? gid, int? mode, FileKind type, bool? overwrite)
        {
            System.Diagnostics.Contracts.Contract.Requires(type != FileKind.Unknown, "FileKind.Unknown is not valid.");

            IRestRequest request = new RestRequest($"/{Client.Version}/containers/{Name}/files?path={path}");
            if (uid.HasValue)
                request.AddParameter("X-LXD-uid", uid.Value);
            if (gid.HasValue)
                request.AddParameter("X-LXD-gid", gid.Value);
            if (mode.HasValue)
                request.AddParameter("X-LXD-mode", mode.Value);
            if (type != FileKind.Unknown)
                request.AddParameter("X-LXD-type", type == FileKind.Directory ? "directory" : (type == FileKind.File ? "file" : "symlink"));
            if (overwrite.HasValue)
                request.AddParameter("X-LXD-write", overwrite.Value);

            request.AddFile("", content, "");
            IRestResponse restResponse = API.Post(request);
            return StandardResponse.Parse(restResponse);
        }

        public StandardResponse DeleteFile(string path)
        {
            IRestRequest request = new RestRequest($"/{Client.Version}/containers/{Name}/files?path={path}");
            request.AddParameter("path", path, ParameterType.UrlSegment);
            IRestResponse restResponse = API.Delete(request);
            return StandardResponse.Parse(restResponse);
        }
    }
}
