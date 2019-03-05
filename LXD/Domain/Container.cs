using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace LXD.Domain
{
    public partial class Container : RemoteObject
    {
        public string Architecture;
        public Dictionary<string, string> Config;
        public DateTime CreatedAt;
        public Dictionary<string, Dictionary<string, string>> Devices;
        public bool Ephemaral;
        public Dictionary<string, string> ExpandedConfig;
        public Dictionary<string, Dictionary<string, string>> ExpandedDevices;
        public string Name;
        public string[] Profiles;
        public bool Stateful;
        public string Status;
        public int StatusCode;

        public JToken Start(int timeout = 30, bool stateful = false)
        {
            ContainerStatePut payload = new ContainerStatePut()
            {
                Action = "start",
                Timeout = timeout,
                Stateful = stateful,
            };

            JToken response = API.Put($"/{Client.Version}/containers/{Name}/state", payload);
            return API.WaitForOperationComplete(response);
        }

        public JToken Stop(int timeout = 30, bool force = false, bool stateful = false)
        {
            ContainerStatePut payload = new ContainerStatePut()
            {
                Action = "stop",
                Timeout = timeout,
                Force = force,
                Stateful = stateful,
            };

            JToken response = API.Put($"/{Client.Version}/containers/{Name}/state", payload);
            return API.WaitForOperationComplete(response);
        }


        public JToken Restart(int timeout = 30, bool force = false)
        {
            ContainerStatePut payload = new ContainerStatePut()
            {
                Action = "restart",
                Timeout = timeout,
                Force = force,
            };

            JToken response = API.Put($"/{Client.Version}/container/{Name}/state", payload);
            return API.WaitForOperationComplete(response);
        }

        public JToken Freeze(int timeout = 30)
        {
            ContainerStatePut payload = new ContainerStatePut()
            {
                Action = "freeze",
                Timeout = timeout,
            };

            JToken response = API.Put($"/{Client.Version}/container/{Name}/state", payload);
            return API.WaitForOperationComplete(response);
        }

        public JToken Unfreeze(int timeout = 30)
        {
            ContainerStatePut payload = new ContainerStatePut()
            {
                Action = "unfreeze",
                Timeout = timeout,
            };

            JToken response = API.Put($"/{Client.Version}/container/{Name}/state", payload);
            return API.WaitForOperationComplete(response);
        }

        public struct ContainerStatePut
        {
            public string Action;
            public int Timeout;
            public bool Force;
            public bool Stateful;
        }

        public Task<ContainerExecResult> Exec(string[] command,
            Dictionary<string, string> environment = null,
            bool waitForWebSocket = true,
            bool recordOutput = false,
            bool interactive = true,
            int width = 80,
            int height = 25)
        {
            ContainerExec exec = new ContainerExec()
            {
                Command = command,
                Environment = environment,
                WaitForWebSocket = waitForWebSocket,
                RecordOutput = recordOutput,
                Interactive = interactive,
                Width = width,
                Height = height,
            };

            return Exec(exec);
        }

        public Task<ContainerExecResult> Exec(ContainerExec exec)
        {
            JToken response = API.Post($"/{Client.Version}/containers/{Name}/exec", exec);
            string operationUrl = response.Value<string>("operation");
            return ContainerExecResult.Create(API, exec, response, operationUrl);
        }

        public struct ContainerExec
        {
            public string[] Command;
            public Dictionary<string, string> Environment;
            [JsonProperty("wait-for-websocket")]
            public bool WaitForWebSocket;
            [JsonProperty("record-output")]
            public bool RecordOutput;
            public bool Interactive;
            public int Width;
            public int Height;
        }

        public class ContainerExecResult
        {
            public ContainerExec ExecSettings { get; private set; }

            protected ContainerExecResult(ContainerExec exec)
            {
                ExecSettings = exec;
            }

            public static async Task<ContainerExecResult> Create(API API, ContainerExec exec, JToken response, string operationUrl) {
                if (exec.WaitForWebSocket)
                {
                    response = API.Get(operationUrl);
                    return await ContainerExecResultWithWebSockets.Create(API, exec, response, operationUrl);
                }
                else if (exec.Interactive == false && exec.RecordOutput)
                {
                    response = API.Get(operationUrl);
                    return ContainerExecResultWithRecords.Create(API, exec, response, operationUrl);
                }
                else
                {
                    API.WaitForOperationComplete(response);
                    return new ContainerExecResult(exec);
                }
            }

            public class ContainerExecResultWithWebSockets : ContainerExecResult, IDisposable
            {
                private ClientWebSocket[] WebSockets { get; set; }

                public ClientWebSocket StandardOutput => WebSockets?.Length >= 3 ? WebSockets[1] : null;
                public ClientWebSocket StandardInput => WebSockets?.Length >= 1 ? WebSockets[0] : null;
                public ClientWebSocket StandardError => WebSockets?.Length >= 4 ? WebSockets[2] : null;
                public ClientWebSocket Control => WebSockets?.Length >= 2 ? WebSockets[WebSockets.Length - 1] : null;

                protected ContainerExecResultWithWebSockets(ContainerExec exec) : base(exec) { }

                public static async new Task<ContainerExecResult> Create(API API, ContainerExec exec, JToken response, string operationUrl)
                {
                    var result = new ContainerExecResultWithWebSockets(exec);
                    var webSocketStrings = exec.Interactive ? new[] { "0", "control" } : new[] { "0", "1", "2", "control" };
                    var sockets = new List<ClientWebSocket>();
                    foreach (var i in webSocketStrings)
                    {
                        string fdsSecret = response.SelectToken($"metadata.metadata.fds.{i}").Value<string>();
                        string wsUrl = $"{API.BaseUrlWebSocket}{operationUrl}/websocket?secret={fdsSecret}";
                        sockets.Add(await ClientWebSocketExtensions.CreateAndConnectAsync(wsUrl, API));
                    }
                    result.WebSockets = sockets.ToArray();
                    return result;
                }

                public async Task CloseAsync() => await CloseAsync(System.Threading.CancellationToken.None);

                public async Task CloseAsync(System.Threading.CancellationToken cancellationToken)
                {
                    if (WebSockets != null)
                        foreach (var ws in WebSockets)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken);
                        }
                }

                public void Dispose()
                {
                    if(WebSockets != null)
                        foreach(var ws in WebSockets)
                        {
                            ws?.Dispose();
                        }
                }
            }

            public class ContainerExecResultWithRecords : ContainerExecResult
            {
                protected ContainerExecResultWithRecords(ContainerExec exec) : base(exec) { }

                protected string[] RecordUrls;
                protected API API;
                public int ReturnCode { get;  private set; }

                private async Task<string> GetOutput(int index)
                {
                    var t = API.Timeout;
                    API.Timeout = System.Threading.Timeout.Infinite;
                    var ret = await API.ExecuteTaskAsync(new RestRequest(RecordUrls[index]));
                    API.Timeout = t;
                    return ret.Content;
                }

                public Task<string> GetStandardOutput() => GetOutput(0);
                public Task<string> GetStandardError() => GetOutput(1);

                public static new ContainerExecResult Create(API api, ContainerExec exec, JToken response, string operationUrl)
                {
                    var result = new ContainerExecResultWithRecords(exec);
                    result.API = api;
                    response = response.SelectToken("metadata");
                    if (response.SelectToken("status_code").Value<int>() == 103)
                    {
                        var t = api.Timeout;
                        string op_id = response.SelectToken("id").Value<string>();
                        api.Timeout = System.Threading.Timeout.Infinite;
                        response = api.Get($"/1.0/operations/{op_id}/wait").SelectToken("metadata");
                        api.Timeout = t;
                    }

                    result.ReturnCode = response.SelectToken("metadata.return").Value<int>();
                    result.RecordUrls = (new[] { "1", "2" }).Select(s =>
                        response.SelectToken($"metadata.output.{s}").Value<string>()
                    ).ToArray();
                    return result;
                }
            }
        }


        public ContainerState State => API.Get<ContainerState>($"/{Client.Version}/containers/{Name}/state");
        public Collection<object> Logs => new Collection<object>(API, $"/{Client.Version}/containers/{Name}/logs");
        public Collection<Container> Snapshots => new Collection<Container>(API, $"/{Client.Version}/containers/{Name}/snapshots");
    }
}
