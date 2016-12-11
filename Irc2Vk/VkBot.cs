using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Irc2Vk
{
    class VkBot : IDisposable
    {
        private VkNet.VkApi _api;
        private IrcListener _irc;
        private bool Stop { get; set;}
        private System.Threading.Thread _readThread;
        private System.Threading.Thread _sendThread;

        private ConcurrentQueue<Message> NewMessages { get; set; }

        public event Action<long> RecivedMessageFromUid;
        private void OnRecivedMessageFromUid(long uid)
        {
            RecivedMessageFromUid?.Invoke(uid);
        }

        private async void ReadUserMessagesCycle()
        {
            while (!Stop)
            {
                try
                {
                    VkNet.Model.LongPollServerResponse longPollServer;
                    lock (_api)
                        longPollServer = _api.Messages.GetLongPollServer(needPts: true);
                    var maxMessagesId = 0L;
                    var pts = longPollServer.Pts;
                    while (!Stop)
                    {
                        var @params = new VkNet.Model.RequestParams.MessagesGetLongPollHistoryParams()
                        {
                            Ts = longPollServer.Ts,
                            Pts = pts,
                            MaxMsgId = maxMessagesId
                        };
                        VkNet.Model.LongPollHistoryResponse history;
                        lock (_api)
                        {
                            history = _api.Messages.GetLongPollHistory(@params);
                            pts = history.NewPts;
                        }
                        
                        foreach(var msg in history.Messages)
                        {
                            Message message;
                            if(!msg.UserId.HasValue || msg.Type == VkNet.Enums.MessageType.Sended || !msg.Id.HasValue)
                                continue;
                            maxMessagesId = msg.Id.Value;
                            message.UserIds = new List<long>() { msg.UserId.Value};
                            message.Msg = msg.Body;
                            message.Attachments = new List<string>();
                            Message? res;
                            lock (_irc)
                                if (msg.UserId != null)
                                    OnRecivedMessageFromUid(msg.UserId.Value);
                                res = _irc.Send(message); 
                            if (res.HasValue)
                                NewMessages.Enqueue(res.Value);
                        }
                    }
                } catch (VkNet.Exception.VkApiException exc)
                {}
                await Task.Delay(new TimeSpan(0, 0, 5));
            }
        }

        private async void SendMessagesCycle()
        {
            while (!Stop)
            {
                Message msg;
                if (!NewMessages.TryDequeue(out msg) || msg.Msg==null ||msg.Msg.Length == 0)
                    continue;
                var succeded = false;
                while (!succeded)
                {
                    try
                    {
                        var @params = new VkNet.Model.RequestParams.MessagesSendParams
                        {
                            Message = msg.Msg
                        };
                        switch (msg.UserIds.Count())
                        {
                            case 1:
                                @params.UserId = msg.UserIds.First();
                                break;
                            case 0:
                                return;
                            default:
                                @params.UserIds = msg.UserIds;
                                break;
                        }

                        lock (_api)
                        {
                            _api.Messages.Send(@params);
                        }
                        succeded = true;
                    }
                    catch (VkNet.Exception.VkApiException ex)
                    {
                        var error = ex.Message;
                    }
                    catch (ArgumentException ex)
                    {
                        var error = ex.Message;
                        var param = ex.ParamName;
                    }
                }
                await Task.Delay(new TimeSpan(0, 0, 1));
            }
        }

        public VkBot(VkNet.VkApi api, IrcListener irc)
        {
            _api = api;
            _irc = irc;
            _irc.MessagesHistoryUpdated += SetNewMessages;
            _readThread = new System.Threading.Thread(ReadUserMessagesCycle);
            _sendThread = new System.Threading.Thread(SendMessagesCycle);
            _sendThread.Start();
            _readThread.Start();
            NewMessages = new ConcurrentQueue<Message>();
            
        }

        private void SetNewMessages(IEnumerable<long> ids, IEnumerable<string> messages)
        {
            var enumerable = ids as IList<long> ?? ids.ToList();
            var values = messages as IList<string> ?? messages.ToList();
            if (!enumerable.Any() || !values.Any())
                return;
            NewMessages.Enqueue(new Message
            {
                UserIds = new List<long>(enumerable),
                Attachments = new List<string>(),
                Msg = string.Join("\n" ,values)
            });
        }

        public void Dispose()
        {
            Stop = true;
            _readThread.Join();
            _sendThread.Join();
        }
    }
}
