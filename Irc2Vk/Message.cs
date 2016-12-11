using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Irc2Vk
{

    struct Message
    {
        public IEnumerable<long> UserIds;
        public string Msg;
        public IEnumerable<string> Attachments;
    }
}
