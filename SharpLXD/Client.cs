using Newtonsoft.Json.Linq;
using System;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using SharpLXD.Domain;
using System.Collections.Generic;
using Newtonsoft.Json;
using RestSharp;

namespace SharpLXD
{
    public class Client
    {
        public const string Version = "1.0";

        public API API;

        public bool Trusted => API.Get($"/{Version}").SelectToken("metadata.auth").Value<string>() == "trusted";

        public Collection<Certificate> Certificates { get; private set; }
        public Collection<Container> Containers { get; private set; }
        public Collection<Image> Images { get; private set; }
        public Collection<Network> Networks { get; private set; }
        public Collection<Operation> Operations { get; private set; }
        public Collection<Profile> Profiles { get; private set; }


        public Client(string apiEndpoint, X509Certificate2 clientCertificate, bool verify = false)
        {
            API = new API(apiEndpoint, clientCertificate, verify);

            // Verify connection.
            API.Get($"/{Version}");

            Certificates = new Collection<Certificate>(API, $"/{Version}/certificates");
            Containers = new Collection<Container>(API, $"/{Version}/containers");
            Images = new Collection<Image>(API, $"/{Version}/images");
            Networks = new Collection<Network>(API, $"/{Version}/networks");
            Operations = new Collection<Operation>(API, $"/{Version}/operations");
            Profiles = new Collection<Profile>(API, $"/{Version}/profiles");

            // Task.Run(() => GetEventsAsync());
        }

        public Client(string apiEndpoint, string clientCertificateFilename, string password, bool verify = false)
            : this(apiEndpoint, new X509Certificate2(clientCertificateFilename, password), verify)
        {
        }

        public Client(string apiEndpoint, bool verify = false)
            : this(apiEndpoint, null, verify)
        {
        }

        public delegate void NewEventHandler(object sender, EventArgs e);

        public event NewEventHandler NewEvent;

        private async Task GetEventsAsync()
        {
            using (ClientWebSocket ws = new ClientWebSocket())
            {
                string url = API.BaseUrl.AbsoluteUri.Replace("http", "ws") + $"{Version}/events";
                await ws.ConnectAsync(new Uri(url), CancellationToken.None);

                while (ws.State == WebSocketState.Open)
                {
                    StringBuilder msg = new StringBuilder();
                    byte[] buffer = new byte[1024];
                    WebSocketReceiveResult receiveResult;
                    do
                    {
                        receiveResult = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                        msg.Append(Encoding.UTF8.GetString(buffer, 0, receiveResult.Count));
                    } while (!receiveResult.EndOfMessage);

                    Console.WriteLine(msg);
                    // NewEvent?.Invoke(wsEvent, msg);
                    NewEvent?.Invoke(ws, EventArgs.Empty);
                }
            }
        }
        
        public StandardResponse PostContainer(PostContainerParameter param)
        {
            IRestRequest restRequest = new RestRequest($"/{Version}/containers");
            restRequest.AddParameter("application/json", param.GetJsonText(), ParameterType.RequestBody);
            return StandardResponse.Parse(API.Post(restRequest));
        }
    }
}
