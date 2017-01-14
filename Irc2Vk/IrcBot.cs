using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IrcDotNet;

namespace Irc2Vk
{
    class IrcBot : IrcDotNet.StandardIrcClient
    {
        public enum State { Connected, Banned, Disconnected }
        public DateTime LastActivity { get; private set; }
        private TimeSpan _maxAllowedInactiveInteral = TimeSpan.FromHours(1);

        public event Action<IrcBot> ClientInactive;
        protected virtual void OnClientInactive(IrcBot obj)
        {
            ClientInactive?.Invoke(obj);
        }

        public string Nickname { get; set; } = null;


        public string GetMessages()
        {
            lock (_messages)
            {
                var res = string.Join("\\n\\n", _messages);
                _messages.Clear();
                return res;
            }
        }

        private string Filter(string text)
        {
            if (text == null) return "";
            var pattern = $"({char.ConvertFromUtf32(3)}[0-9]{{0,2}},?[0-9]{{0,2}})|{char.ConvertFromUtf32(15)}";
            return Regex.Replace(text, pattern, "");
        }

        private void EnqueMessage(object sender, IrcRawMessageEventArgs e)
        {
            lock (_messages)
            {
                if (e.Message.Source == null) return;
                if (e.Message.Source?.Name != Nickname && e.Message.Command.Equals("PRIVMSG"))
                    _messages.Add($"{Filter(e.Message.Source?.Name)}: {Filter(e.Message.Parameters[1])}");
            }

            if (DateTime.Now - LastActivity > _maxAllowedInactiveInteral)
                OnClientInactive(this);
        }
        
        private string _channel;
        private string _hostname;
        private int _port;
        private List<string> _messages;

        public State CurrentState { get; private set; }

        public IrcBot(string hostname, string channel, int port=6667) 
        {
            CurrentState = State.Disconnected;
            
            _channel = channel;
            _hostname = hostname;
            _port = port;
            _messages = new List<string>();
            LastActivity = DateTime.Now;

        }

        public void Send(string msg)
        {
            if (CurrentState != State.Connected)
                return;

            LastActivity = DateTime.Now;
            SendMessagePrivateMessage(new List<string>() {_channel}, msg);

        } 

        public void Connect()
        {
            if (Nickname == null)
                return;
            Connect(new DnsEndPoint(_hostname, _port), false, new IrcUserRegistrationInfo() {NickName = Nickname, UserName = Nickname, RealName = Nickname});
            RawMessageReceived += EnqueMessage;
            Registered += (sender, args) => CurrentState = State.Connected;
            Disconnected += OnDisconnected;
            Channels.Join(new List<string>() { _channel });

        }

        private void OnDisconnected(object sender, EventArgs eventArgs)
        {
            RawMessageReceived -= EnqueMessage;
            CurrentState = State.Disconnected;
            lock (_messages)
            {
                _messages.Clear();
            }
        }
    }
}
