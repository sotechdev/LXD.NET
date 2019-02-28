using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace LXD
{
    public class StandardResponse
    {
        public string ErrorContent { get; private set; }
        public int ErrorCode { get; private set; }

        internal StandardResponse(string errorContent, int errorCode)
        {
            ErrorContent = errorContent;
            ErrorCode = errorCode;
        }

        public static StandardResponse Parse(IRestResponse restResponse)
        {
            if (restResponse.ContentType != "application/json")
                return new InvalidResponse();
            return Parse(restResponse.Content);
        }

        public static StandardResponse Parse(string response)
        {
            JToken token = JToken.Parse(response);
            JToken statusCode = token.SelectToken("status_code");
            if (statusCode == null)
                return new Error(token.SelectToken("error").Value<string>(), token.SelectToken("error_code").Value<int>());
            else
                return new Success(token.SelectToken("error").Value<string>(), token.SelectToken("error_code").Value<int>(), statusCode.Value<int>());
        }

        public class Error : StandardResponse
        {
            internal Error(string errorContent, int errorCode) : base(errorContent, errorCode) { }
        }

        public class InvalidResponse : StandardResponse
        {
            internal InvalidResponse() : base(null, 404) { }
        }

        public class Success : StandardResponse
        {
            public int StatusCode { get; private set; }
            internal Success(string errorContent, int errorCode, int statusCode) : base(errorContent, errorCode)
            {
                StatusCode = statusCode;
            }
        }
    }
}
