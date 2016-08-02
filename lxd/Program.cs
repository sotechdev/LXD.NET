﻿using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Threading;
using System.Net.WebSockets;

namespace lxd
{
	class ContainerExecDTO
	{
		public string[] command = "cat /etc/issue".Split(' ');
		[JsonProperty("wait-for-websocket")]
		public bool waitForWebsocket = true;
		public bool interactive = true;
	}

    class Program
    {
		const string serviceAddr = "ubuntu:8443";

        static void Main(string[] args)
        {

            RestRequest request;
            IRestResponse response;

            // Assert authrization.
            request = new RestRequest("/1.0");
            response = client.Execute(request);
            string auth = JToken.Parse(response.Content).SelectToken("metadata.auth").Value<string>();
            Contract.Assert(auth == "trusted");

            // Exec
            request = new RestRequest("/1.0/containers/alpine/exec", Method.POST);
			request.JsonSerializer = new JsonNetSerializer();
			request.AddJsonBody(new ContainerExecDTO());
			response = client.Execute(request);

            string operationUrl = JToken.Parse(response.Content).Value<string>("operation");
            Contract.Assert(operationUrl != null);

			// Get fds secret.
			request = new RestRequest(operationUrl);
			response = client.Execute(request);

			string secret = JToken.Parse(response.Content).SelectToken("metadata.metadata.fds.0").Value<string>();

			// Connect websocket.
			string wsUrl = $"wss://{serviceAddr}/{operationUrl}/websocket?secret={secret}";
			//string wsUrl = "wss://echo.websocket.org";

			string stdout = ReadAllLinesFromWebSocketAsync(wsUrl).Result;

			Console.WriteLine(stdout);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

		static async Task<string> ReadAllLinesFromWebSocketAsync(string url)
		{
			const int WebSocketChunkSize = 1024;

			StringBuilder stdout = new StringBuilder();

			using (ClientWebSocket ws = new ClientWebSocket())
			{
				await ws.ConnectAsync(new Uri(url), CancellationToken.None);

				byte[] buffer = new byte[WebSocketChunkSize];
				WebSocketReceiveResult result;
				do
				{
					result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

					string partialMsg = Encoding.UTF8.GetString(buffer, 0, result.Count);
					stdout.Append(partialMsg);
				} while (!result.EndOfMessage);
			}

			return stdout.ToString();
		}
    }
}
