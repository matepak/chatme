using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chatmee_clientserver
{
    [Serializable]
    class MessageFrame
    {
        public string Sender { get; set; } = null;
        public string Destination { get; set; } = null;
        public string Command { get; set; } = null;
        public string Param { get; set; } = null;
        public string Data { get; set; } = null;
        public List<string> ConnectedUsers { get; set; } = null;
    }
}
