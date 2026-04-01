using System;
using System.Text;
using UnityEngine.Networking;

namespace Kyalio.Services
{
    /// <summary>
    /// Custom download handler for Server-Sent Events (SSE) streams.
    /// Parses text/event-stream format and fires <see cref="OnEvent"/> for each
    /// complete event block (double-newline terminated).
    /// </summary>
    internal sealed class SseDownloadHandler : DownloadHandlerScript
    {
        public event Action<string, string> OnEvent; // (eventName, dataJson)

        private readonly StringBuilder _buffer = new StringBuilder();

        public SseDownloadHandler(Action<string, string> onEvent) : base(new byte[4096])
        {
            OnEvent = onEvent;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            _buffer.Append(Encoding.UTF8.GetString(data, 0, dataLength));
            FlushEvents();
            return true;
        }

        protected override string GetText() => _buffer.ToString();

        private void FlushEvents()
        {
            var text = _buffer.ToString();
            int pos;
            while ((pos = text.IndexOf("\n\n", StringComparison.Ordinal)) >= 0)
            {
                ParseBlock(text.Substring(0, pos));
                text = text.Substring(pos + 2);
            }
            _buffer.Clear();
            _buffer.Append(text);
        }

        private void ParseBlock(string block)
        {
            string eventName = "message";
            string data = null;
            foreach (var line in block.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.StartsWith("event:"))
                    eventName = trimmed.Substring(6).Trim();
                else if (trimmed.StartsWith("data:"))
                    data = trimmed.Substring(5).Trim();
            }
            if (data != null)
                OnEvent?.Invoke(eventName, data);
        }
    }
}
