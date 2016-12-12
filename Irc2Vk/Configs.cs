using System;

namespace Irc2Vk
{
    [Serializable]
    public struct VkBotConfig
    {
        public ulong AppId;
        public string Email;
        public string Pass;
    }

    [Serializable]
    public struct IrcConfig
    {
        public string Host;
        public string Pass;
        public ulong Port;
        public string Channel;
    }
}