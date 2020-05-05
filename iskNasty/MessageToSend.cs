using System;
using TeleSharp.TL;

namespace iskNasty
{
    public class MessageToSend
    {
        public object target;
        public string text;

        public MessageToSend(Object Target, string Text)
        {
            target = Target;
            text = Text;
        }
    }
}
