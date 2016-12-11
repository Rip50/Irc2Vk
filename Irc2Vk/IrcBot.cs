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
        public enum State { NicknameRequired, Connected, Banned, Disconnected }
        private string _nickname = "";
        public string Nickname
        {
            get { return _nickname; }
            set {
                _nickname = value;
                _client.ChangeName(_nickname);
                try
                {
                    _client.LogIn(_nickname, _nickname, _nickname);
                    _client.GotMessage += EnqueMessage;
                    CurrentState = State.Connected;
                } catch(Exception)
                {
                    CurrentState = State.Disconnected;
                }
            }
        }

        public string GetMessages()
        {
            lock (_messages)
            {
                var res = string.Join("\n", _messages);
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


        }

        public void Send(string msg)
        {
            if (CurrentState == State.Connected)
                _client.Message(_channel, msg);
        } 

        public void Connect()
        {
            _client = new NetIrc2.IrcClient();
            _client.Connect(_hostname, _port);
            CurrentState = State.NicknameRequired;
        }

        public void Disconnect()
        {
            _client.LogOut("Bye");
            CurrentState = State.Disconnected;
        }
    }
}
