using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace LXD
{
    public class StandardResponse<T>
    {
        public T Metadata { get; private set; }
        public static StandardResponse<T> Parse(IRestResponse restResponse, Func<JToken, T> MetadataResolver)
        {
            if (restResponse.ContentType != "application/json")
                return new InvalidResponse<T>();
            return Parse(restResponse.Content, MetadataResolver);
        }

        public static StandardResponse<T> Parse(string response, Func<JToken, T> MetadataResolver)
            => Parse(JToken.Parse(response), MetadataResolver);

        public static StandardResponse<T> Parse(JToken response, Func<JToken, T> MetadataResolver)
        {
            var result = response.SelectToken("type").Value<string>() switch
            {
                "error" => (StandardResponse<T>)new Error<T>(response.SelectToken("error").Value<string>(), response.SelectToken("error_code").Value<int>()),
                "sync" => (StandardResponse<T>)new Success<T>(response.SelectToken("status_code").Value<int>()),
                "async" => (StandardResponse<T>)new BackgroundOperation<T>(response.SelectToken("operation").Value<string>(), response.SelectToken("status_code").Value<int>()),
                _ => (StandardResponse<T>)new InvalidResponse<T>()
            };
            result.Metadata = MetadataResolver(response.SelectToken("metadata"));
            return result;
        }

#pragma warning disable CS0693
        public class Error<T> : StandardResponse<T>
        {
            public string ErrorContent { get; private set; }
            public int ErrorCode { get; private set; }
            internal Error(string errorContent, int errorCode)
            {
                ErrorContent = errorContent;
                ErrorCode = errorCode;
            }
        }

        public class InvalidResponse<T> : StandardResponse<T>
        { }

        public class Success<T> : StandardResponse<T>
        {
            public int StatusCode { get; private set; }
            internal Success(int statusCode)
            {
                StatusCode = statusCode;
            }
        }

        public class BackgroundOperation<T> : Success<T>
        {
            public string Operation { get; private set; }
            internal BackgroundOperation(string operation, int statusCode) : base(statusCode)
            {
                Operation = operation;
            }

            public JToken Wait(API api, int? timeout)
            {
                var t = api.Timeout;
                api.Timeout = System.Threading.Timeout.Infinite;
                var res = api.Get(timeout == null ? Operation : Operation + "?timeout=" + timeout.Value.ToString());
                api.Timeout = t;
                return res;
            }
        }
#pragma warning restore CS0693
    }

    public class StandardResponse
    {
        public static StandardResponse Parse(IRestResponse restResponse)
        {
            if (restResponse.ContentType != "application/json")
                return new InvalidResponse();
            return Parse(restResponse.Content);
        }

        public static StandardResponse Parse(string response)
            => Parse(JToken.Parse(response));

        public static StandardResponse Parse(JToken response)
        {
            return response.SelectToken("type").Value<string>() switch
            {
                "error" => (StandardResponse)new Error(response.SelectToken("error").Value<string>(), response.SelectToken("error_code").Value<int>()),
                "sync" => (StandardResponse)new Success(response.SelectToken("status_code").Value<int>()),
                "async" => (StandardResponse)new BackgroundOperation(response.SelectToken("operation").Value<string>(), response.SelectToken("status_code").Value<int>()),
                _ => (StandardResponse)new InvalidResponse()
            };
        }

        public class Error : StandardResponse
        {
            public string ErrorContent { get; private set; }
            public int ErrorCode { get; private set; }
            internal Error(string errorContent, int errorCode)
            {
                ErrorContent = errorContent;
                ErrorCode = errorCode;
            }
        }

        public class InvalidResponse : StandardResponse
        { }

        public class Success : StandardResponse
        {
            public int StatusCode { get; private set; }
            internal Success(int statusCode)
            {
                StatusCode = statusCode;
            }
        }

        public class BackgroundOperation : Success
        {
            public string Operation { get; private set; }
            internal BackgroundOperation(string operation, int statusCode) : base(statusCode)
            {
                Operation = operation;
            }
            public JToken Wait(API api, int? timeout)
            {
                var t = api.Timeout;
                api.Timeout = System.Threading.Timeout.Infinite;
                var res = api.Get(timeout == null ? Operation : Operation + "?timeout=" + timeout.Value.ToString());
                api.Timeout = t;
                return res;
            }
        }
    }
}
