using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using NetIrc2;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Irc2Vk
{
    class IrcListener : IDisposable
    {
        private System.Timers.Timer _updateTimer;
        private string _hostname;
        private string _channel;
        private int _port;

        public event Action<IEnumerable<long>, IEnumerable<string>> MessagesHistoryUpdated;
        
        private List<Message> MessagesHistory { get; set; }
        private ConcurrentDictionary<long, IrcBot> Bots { get; set; }
        public event Action<long> UserBanned;

        private void OnUserBanned(long uid)
        {
            UserBanned?.Invoke(uid);
        }

        private void OnMessagesHistoryUpdated(IEnumerable<long> uids, IEnumerable<string> msgs  )
        {
            MessagesHistoryUpdated?.Invoke(uids, msgs);
        }

        public IrcListener(string hostname, string channel, string listenerNick, int port = 6667)
        {
            _hostname = hostname;
            _channel = channel;
            _port = port;
            Bots = new ConcurrentDictionary<long, IrcBot>();
            MessagesHistory = new List<Message>();
            _updateTimer = new System.Timers.Timer(1000);
            _updateTimer.Elapsed += Update;
            _updateTimer.Start();
        }

        private void Update(object sender, ElapsedEventArgs e)
        {
            foreach (var bot in Bots.Where(kv => kv.Value.CurrentState == IrcBot.State.Connected))
            {
                var msgs = bot.Value.GetMessages();
                if(msgs.Length!=0)
                    OnMessagesHistoryUpdated(new List<long>() {bot.Key}, new List<string>() {msgs} );
            }
        }
        

        internal void CreateNewBot(long uid)
        {
            if (Bots.Keys.Contains(uid))
                return;
            Bots[uid] = new IrcBot(_hostname, _channel, _port);
        }

        internal void RemoveBot(long uid)
        {
            if (!Bots.Keys.Contains(uid))
                return;
            IrcBot bot;
            if(Bots.TryRemove(uid, out bot))
            {
                bot.Disconnect();
            }
            
        }

        internal Message? Send(Message message)
        {
            if(!Bots.Keys.Contains(message.UserIds.First()))
                return null;
            var bot = Bots[message.UserIds.First()];
            if (bot.CurrentState == IrcBot.State.Disconnected)
            {
                bot.Connect();
                return new Message()
                {
                    UserIds = message.UserIds,
                    Msg = "Введите ник для чата в IRC",
                    Attachments = new List<string>()
                };
            }
            else if (bot.CurrentState == IrcBot.State.NicknameRequired)
            {
                if(!Regex.IsMatch(message.Msg, "[a-zA-Z0-9_]+"))
                    return new Message()
                    {
                        UserIds = message.UserIds,
                        Msg = "В качестве ника разрешено использовать только буквы латинского алфавита, цифры и знак '_'",
                        Attachments = new List<string>()
                    };
                bot.Nickname = message.Msg;
            } else if(bot.CurrentState == IrcBot.State.Banned) {
                OnUserBanned(message.UserIds.First());
            } else {
                bot.Send(message.Msg);
            }
            return null;
        }
        
        public void Dispose()
        {
        }
    }
}
