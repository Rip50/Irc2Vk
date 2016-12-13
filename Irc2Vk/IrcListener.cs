﻿using System;
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
    //Конфиги
    [Serializable]
    public struct IrcConfig
    {
        public string Host;
        public string Pass;
        public int Port;
        public string Channel;
        public double UpdateMsPeriod;
        public ushort MaxAttachmentsCount;
    }

    [Serializable]
    public struct ClientCfg
    {
        public long Uid;
        public string Nickname;
        public DateTime LastActivity;
    }

    [Serializable]
    public struct ClientsConfig
    {
        public List<ClientCfg> Clients;
    }

    //Класс, осуществляющий управление ботами
    class IrcListener : IDisposable
    {
        private readonly System.Timers.Timer _updateTimer;
        
        public IrcConfig Config { get; set; }

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

        public IrcListener(IrcConfig config, ClientsConfig? knownClients = null)
        {
            Config = config;
            Bots = new ConcurrentDictionary<long, IrcBot>();
            MessagesHistory = new List<Message>();
            _updateTimer = new System.Timers.Timer(Config.UpdateMsPeriod);
            _updateTimer.Elapsed += Update;

            if (knownClients?.Clients == null) return;
            foreach (var client in knownClients?.Clients)
            {
                var uid = client.Uid;
                CreateNewBot(uid);
                if (!Bots.Keys.Contains(uid))
                    continue;
                Bots[uid].Nickname = client.Nickname;
                Bots[uid].Connect();
            }
        }

        public void Start()
        {
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
            Bots[uid] = new IrcBot(Config.Host, Config.Channel, Config.Port);
            Bots[uid].ClientInactive += OnClientInactive;
        }

        private void OnClientInactive(IrcBot ircBot)
        {
            var inactive = Bots.First(kv => kv.Value == ircBot);
            inactive.Value.Disconnect();
            MessagesHistory.Add(new Message()
            {
                UserIds = new List<long>() { inactive.Key}, 
                Msg = "Вы были отключены за длительное бездействие"
            });
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
            if (!Bots.Keys.Contains(message.UserIds.First()))
            {
                CreateNewBot(message.UserIds.First());
                return new Message()
                {
                    Msg = $"Введите ник для участия в беседе на канале {Config.Channel}",
                    UserIds = new List<long>() { message.UserIds.First() }
                };
            }

            var bot = Bots[message.UserIds.First()];

            if (bot.Nickname == null)
            {
                if(!Regex.IsMatch(message.Msg, "[a-zA-Z0-9_]+"))
                    return new Message()
                    {
                        UserIds = message.UserIds,
                        Msg = "В качестве ника разрешено использовать только буквы латинского алфавита, цифры и знак '_'."
                    };
                bot.Nickname = message.Msg;
                bot.Connect();
                if (bot.CurrentState == IrcBot.State.Connected)
                {
                    return new Message()
                    {
                        Msg = $"Добро пожаловать в чат, {bot.Nickname}!",
                        UserIds = message.UserIds
                    };
                }
                return new Message()
                {
                    Msg = $"Не удалось подключиться, {bot.Nickname}. Попробуйте позже.",
                    UserIds = message.UserIds
                };
            }

            if(bot.CurrentState == IrcBot.State.Banned) {
                OnUserBanned(message.UserIds.First());
            } else
            {
                bot.Send(message.Msg);
                if (message.Attachments != null)
                {
                    var atts = message.Attachments.Take(Math.Min(message.Attachments.Count(), Config.MaxAttachmentsCount));
                    foreach (var att in atts)
                    {
                        bot.Send(att);
                    }
                }
                if(message.Attachments?.Count()>Config.MaxAttachmentsCount)
                    return new Message()
                    {
                        Msg = $"Число приложений привысило максимально допустимое. Были отосланы {Config.MaxAttachmentsCount} первых приложений.",
                        UserIds = message.UserIds
                    };
            }
            return null;
        }
        
        public void Dispose()
        {
        }
    }
}
