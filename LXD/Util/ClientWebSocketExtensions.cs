using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LXD
{
    public static class ClientWebSocketExtensions
    {
        public static async Task<ClientWebSocket> CreateAndConnectAsync(string url, API API = null)
        {
            ClientWebSocket ws = new ClientWebSocket();
            // LXD sometimes returns '500'. So retry 10 times.
            for (int i = 0; i < 10; ++i) {
                try
                {
                    await ws.ConnectAsync(new Uri(url), CancellationToken.None);
                    break;
                }
                catch(AggregateException ex) when (ex.InnerException is InvalidOperationException)
                {
                    if (((InvalidOperationException)ex.InnerException).Message.Contains("already"))
                        return ws;
                    if (i == 9)
                        throw;
                }
                catch
                {
                    if (i == 9)
                        throw;
                }
                await Task.Delay(500);
            }
            return ws;
        }

        public static async Task<string> ReadLinesAsync(this ClientWebSocket ws)
        {
            StringBuilder sb = new StringBuilder();

            const int WebSocketChunkSize = 1024;
            byte[] buffer = new byte[WebSocketChunkSize];
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                string partialMsg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                sb.Append(partialMsg);
            } while (!result.EndOfMessage && ws.State == WebSocketState.Open);

            return sb.ToString();
        }

        public static async Task WriteAsync(this ClientWebSocket ws, string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await ws.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken: CancellationToken.None);
        }
    }
}
