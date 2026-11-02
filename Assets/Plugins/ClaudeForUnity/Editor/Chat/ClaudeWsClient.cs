using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Core.ClaudeChat
{
    /// <summary>
    /// Minimal WebSocket client for the local companion (ws://127.0.0.1:PORT). The receive loop runs
    /// on a background task; every inbound frame — plus synthetic connect/close notices — is pushed to a
    /// thread-safe queue that the EditorWindow drains on the main thread, so UI code never touches a
    /// background thread. Sends are serialized with a semaphore (ClientWebSocket forbids concurrent sends).
    /// </summary>
    public sealed class ClaudeWsClient
    {
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<string> _inbound = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        /// <summary>Local control messages the client injects into the same queue as server frames.</summary>
        public const string OpenMarker = "{\"t\":\"_open\"}";

        public WebSocketState State => _ws?.State ?? WebSocketState.None;
        public bool IsOpen => _ws != null && _ws.State == WebSocketState.Open;

        public bool TryDequeue(out string message) => _inbound.TryDequeue(out message);

        public void Connect(string url)
        {
            Close("reconnect");
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();
            _ = RunAsync(url, _cts.Token);
        }

        public void Close(string reason = "closed")
        {
            try { _cts?.Cancel(); } catch { }
            try
            {
                if (_ws != null && (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived))
                    _ = _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None);
            }
            catch { }
            _ws = null;
            _cts = null;
        }

        /// <summary>Fire-and-forget send. Safe to call from the main thread.</summary>
        public void Send(string json)
        {
            var ws = _ws;
            var cts = _cts;
            if (ws == null || ws.State != WebSocketState.Open || cts == null) return;
            _ = SendAsync(ws, json, cts.Token);
        }

        private async Task SendAsync(ClientWebSocket ws, string json, CancellationToken ct)
        {
            await _sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Enqueue(Local("_close", $"send failed: {ex.Message}"));
            }
            finally
            {
                try { _sendLock.Release(); } catch { }
            }
        }

        private async Task RunAsync(string url, CancellationToken ct)
        {
            var ws = _ws;
            try
            {
                await ws.ConnectAsync(new Uri(url), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Enqueue(Local("_close", $"connect failed: {ex.Message}"));
                return;
            }

            Enqueue(OpenMarker);

            var buffer = new byte[16 * 1024];
            // Accumulate the raw BYTES of a message and decode UTF-8 once at EndOfMessage. Decoding each
            // 16KB chunk separately would corrupt any multi-byte character split across a chunk boundary
            // (very likely for large frames like a ~200KB blueprint image or long Russian text), producing
            // invalid JSON that the panel then silently drops.
            using var ms = new System.IO.MemoryStream();
            try
            {
                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false);
                            Enqueue(Local("_close", "server closed"));
                            return;
                        }
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (ms.Length > 0)
                        Enqueue(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length));
                }
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                Enqueue(Local("_close", $"receive ended: {ex.Message}"));
            }
        }

        private void Enqueue(string s) => _inbound.Enqueue(s);

        private static string Local(string t, string message)
        {
            // Minimal JSON; message is escaped for quotes/backslashes/newlines.
            var m = message.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
            return $"{{\"t\":\"{t}\",\"message\":\"{m}\"}}";
        }
    }
}
