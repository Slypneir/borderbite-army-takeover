using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Core.EditorBridge
{
    /// <summary>
    /// One-shot diagnostic: performs a WebSocket upgrade handshake against the chat backend using a
    /// RAW TCP socket on Mono's System.Net stack (the same stack ClientWebSocket uses), and logs the
    /// server's raw HTTP response (status line + headers). ClientWebSocket on the Unity editor Mono
    /// swallows the real handshake result into a generic "Unable to connect to the remote server",
    /// so this bypasses it to reveal whether the server returns 101 (our WS client is the problem)
    /// or 401/other (auth/server is the problem).
    ///
    /// The token is read from PlayerPrefs and sent only to the configured backend host.
    /// Run via Tools → Chat → Probe WS Handshake. Output goes to the Console (and console.log).
    /// </summary>
    public static class ChatWsProbe
    {
        // Matches the configured Gateway baseUrl (HttpConfig.asset) + WS path.
        private const string Host = "k8s-trinogba-gatewayi-6ed3da5faa-782315962.ap-southeast-2.elb.amazonaws.com";
        private const int Port = 80;
        private const string Path = "/chat/v1/ws";

        [MenuItem("Tools/Claude for Unity/Probe WS Handshake", false, 20)]
        private static void Probe()
        {
            var token = ReadToken();
            Debug.Log($"[ChatWsProbe] token present = {!string.IsNullOrEmpty(token)} (len {token?.Length ?? 0}). " +
                      $"Connecting raw TCP to {Host}:{Port}{Path} ...");

            try
            {
                using (var client = new TcpClient())
                {
                    var connected = client.BeginConnect(Host, Port, null, null);
                    if (!connected.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(8)))
                    {
                        Debug.LogError("[ChatWsProbe] RAW TCP connect TIMED OUT (Mono cannot reach host on port 80).");
                        return;
                    }

                    client.EndConnect(connected);
                    Debug.Log("[ChatWsProbe] RAW TCP connected OK.");

                    using (var stream = client.GetStream())
                    {
                        var req = new StringBuilder();
                        req.Append($"GET {Path} HTTP/1.1\r\n");
                        req.Append($"Host: {Host}\r\n");
                        req.Append("Upgrade: websocket\r\n");
                        req.Append("Connection: Upgrade\r\n");
                        req.Append("Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n");
                        req.Append("Sec-WebSocket-Version: 13\r\n");
                        if (!string.IsNullOrEmpty(token))
                            req.Append($"Authorization: Bearer {token}\r\n");
                        req.Append("\r\n");

                        var bytes = Encoding.ASCII.GetBytes(req.ToString());
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();

                        stream.ReadTimeout = 8000;
                        var buffer = new byte[8192];
                        var sb = new StringBuilder();
                        var deadline = DateTime.UtcNow.AddSeconds(8);

                        // Read until the end of the HTTP headers (blank line) or timeout.
                        try
                        {
                            while (DateTime.UtcNow < deadline)
                            {
                                if (!stream.DataAvailable)
                                {
                                    Thread.Sleep(50);
                                    if (sb.Length > 0)
                                        break; // got something, no more pending
                                    continue;
                                }

                                int read = stream.Read(buffer, 0, buffer.Length);
                                if (read <= 0)
                                    break;

                                sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
                                if (sb.ToString().Contains("\r\n\r\n"))
                                    break;
                            }
                        }
                        catch (IOException io)
                        {
                            Debug.LogWarning($"[ChatWsProbe] read ended: {io.Message}");
                        }

                        var response = sb.ToString();
                        var headerPart = response.Contains("\r\n\r\n")
                            ? response.Substring(0, response.IndexOf("\r\n\r\n", StringComparison.Ordinal))
                            : response;

                        if (string.IsNullOrEmpty(headerPart))
                            Debug.LogError("[ChatWsProbe] NO RESPONSE from server (connection opened but server sent nothing).");
                        else
                            Debug.Log($"[ChatWsProbe] === RAW SERVER RESPONSE ===\n{headerPart}\n=== END ===");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatWsProbe] RAW probe failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string ReadToken()
        {
            var token = PlayerPrefs.GetString("AuthToken", string.Empty);
            if (!string.IsNullOrEmpty(token))
                return token;

            var json = PlayerPrefs.GetString("AuthorizationData", string.Empty);
            if (string.IsNullOrEmpty(json))
                return string.Empty;

            try { return JObject.Parse(json)["token"]?.ToString() ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
