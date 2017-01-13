using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Irc2Vk
{

    struct Message
    {
        public long UserId;
        public string Msg;
        public IEnumerable<string> Attachments;
    }
}
