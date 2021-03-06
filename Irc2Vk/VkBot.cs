﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VkNet.Model.Attachments;

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
                            message.UserId = msg.UserId.Value;
                            message.Msg = msg.Body;
                            var attachments = new List<string>();
                            foreach (var at in msg?.Attachments)
                            {
                                switch(at.Type.Name)
                                {
                                    case "Photo":
                                        var p = at.Instance as Photo;
                                        attachments.Add($"[img]{p.Photo1280?.AbsoluteUri}");
                                        break;
                                    case "Audio":
                                        var a = at.Instance as Audio;
                                        attachments.Add($"[audio]{a.Url.AbsoluteUri}");
                                        break;
                                    case "Document":
                                        var d = at.Instance as Document;
                                        attachments.Add($"[doc]{d.Url}");
                                        break;
                                    case "Link":
                                        var l = at.Instance as Link;
                                        attachments.Add($"[link]{l.Url.AbsoluteUri}");
                                        break;
                                    default:
                                        break;
                                }
                            }
                            message.Attachments = attachments;
                            Message? res;
                            lock (_irc)
                                res = _irc.Send(message); 
                            if (res.HasValue)
                                NewMessages.Enqueue(res.Value);
                        }
                    }
                } catch (VkNet.Exception.VkApiException exc)
                { }
                catch (NullReferenceException exc)
                { }
                await Task.Delay(new TimeSpan(0, 0, 5));
            }
        }

        private async void SendMessagesCycle()
        {
            var lastSend = DateTime.Now;
            while (!Stop)
            {
                if (NewMessages.IsEmpty)
                {
                    TryAccumulateMessages();
                    continue;
                }
                var newMsgs = NewMessages.ToArray();
                NewMessages = new ConcurrentQueue<Message>();
                var sends = newMsgs.Select(x => $"API.messages.send({{ \"user_id\": {x.UserId},\"message\" : \"{x.Msg}\"}})");
                var request = $"return [{string.Join(",", sends)}];";
                var succeded = false;
                while (!succeded)
                {
                    try
                    {
                        lock (_api)
                        {
                            var diff = DateTime.Now - lastSend;
                            lastSend = DateTime.Now;
                            _api.Execute.Execute(request);

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
                await Task.Delay(TimeSpan.FromSeconds(3.0));
            }
        }

        private void TryAccumulateMessages()
        {
            lock (_irc)
            {
                foreach (var msg in _irc.GetNewMessages())
                {
                    NewMessages.Enqueue(msg);
                }
            }
        }

        public VkBot(VkNet.VkApi api, IrcListener irc)
        {
            _api = api;
            _irc = irc;
            _readThread = new System.Threading.Thread(ReadUserMessagesCycle);
            _sendThread = new System.Threading.Thread(SendMessagesCycle);
            NewMessages = new ConcurrentQueue<Message>();
            
        }

        public void Start()
        {
            _sendThread.Start();
            _readThread.Start();
        }
        
        public void Dispose()
        {
            Stop = true;
            _readThread.Join();
            _sendThread.Join();
        }
    }
}
