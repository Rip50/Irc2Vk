using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NetIrc2.Events;

namespace Irc2Vk
{
    class IrcBot
    {
        public enum State { Connected, Banned, Disconnected }
        public DateTime LastActivity { get; private set; }
        private TimeSpan _maxAllowedInactiveInteral = new TimeSpan(1,0,0);

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
                var res = string.Join("\n\n", _messages);
                _messages.Clear();
                return res;
            }
        }

        private string Filter(string text)
        {
            var pattern = $"({char.ConvertFromUtf32(3)}[0-9]{{0,2}},?[0-9]{{0,2}})|{char.ConvertFromUtf32(15)}";
            return Regex.Replace(text, pattern, "");
        }

        private void EnqueMessage(object sender, ChatMessageEventArgs e)
        {
            lock (_messages)
            {
                if (e.Sender.Nickname != Nickname)
                    _messages.Add($"{Filter(e.Sender.Nickname)}: {Filter(e.Message)}");
            }

            if (DateTime.Now - LastActivity > _maxAllowedInactiveInteral)
                OnClientInactive(this);
        }

        private NetIrc2.IrcClient _client;
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
            _client.Message(_channel, msg);

        } 

        public void Connect()
        {
            _client = new NetIrc2.IrcClient();
            _client.Connect(_hostname, _port);
            if (Nickname == null)
                return;
            _client.LogIn(Nickname, Nickname, Nickname);
            _client.GotMessage += EnqueMessage;
            CurrentState = State.Connected;
        }

        public void Disconnect()
        {
            _client.LogOut("Bye");
            _client.GotMessage -= EnqueMessage;
            CurrentState = State.Disconnected;
            lock (_messages)
            {
                _messages.Clear();
            }
        }


    }
}
