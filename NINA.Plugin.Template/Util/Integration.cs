using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LensAF.Util
{
    public class Integration : ISubscriber, IDisposable
    {
        private IMessageBroker messageBroker;

        public Integration(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;

            this.messageBroker.Subscribe("LensAF.RegisterFocuser", this);
            this.messageBroker.Subscribe("LensAF.GotoFocus", this);
        }

        public void Dispose()
        {
            this.messageBroker.Unsubscribe("LensAF.RegisterFocuser", this);
            this.messageBroker.Unsubscribe("LensAF.GotoFocus", this);
        }

        public async Task OnMessageReceived(IMessage message)
        {
            if (message.Topic == "LensAF.RegisterFocuser")
            {
                LensAF.AddLensConfigIfNecessary((string)message.Content);
            }
            else if (message.Topic == "LensAF.GotoFocus")
            {
                if (LensAF.Focuser.GetInfo().Connected) {
                    LensAF.AddLensConfigIfNecessary(LensAF.Focuser.GetInfo().DisplayName);
                    await LensAF.Focuser.MoveFocuser(LensAF.GetFocusPosition(LensAF.Focuser.GetInfo().DisplayName), new());
                }
            }
        }
    }
}
