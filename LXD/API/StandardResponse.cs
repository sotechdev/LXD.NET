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
        {
            JToken token = JToken.Parse(response);
            string responseType = token.SelectToken("type").Value<string>();
            var result = responseType switch
            {
                "error" => (StandardResponse<T>)new Error<T>(token.SelectToken("error").Value<string>(), token.SelectToken("error_code").Value<int>()),
                "sync" => (StandardResponse<T>)new Success<T>(token.SelectToken("status_code").Value<int>()),
                "async" => (StandardResponse<T>)new BackgroundOperation<T>(token.SelectToken("operation").Value<string>(), token.SelectToken("status_code").Value<int>()),
                _ => (StandardResponse<T>)new InvalidResponse<T>()
            };
            result.Metadata = MetadataResolver(token.SelectToken("metadata"));
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
        {
            JToken token = JToken.Parse(response);
            string responseType = token.SelectToken("type").Value<string>();
            var result = responseType switch
            {
                "error" => (StandardResponse)new Error(token.SelectToken("error").Value<string>(), token.SelectToken("error_code").Value<int>()),
                "sync" => (StandardResponse)new Success(token.SelectToken("status_code").Value<int>()),
                "async" => (StandardResponse)new BackgroundOperation(token.SelectToken("operation").Value<string>(), token.SelectToken("status_code").Value<int>()),
                _ => (StandardResponse)new InvalidResponse()
            };
            return result;
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
        }
    }
}
