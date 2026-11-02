using System;

namespace Operations_Control.Models
{
    public sealed class JogRequestEventArgs : EventArgs
    {
        public int Motor { get; private set; }
        public int Direction { get; private set; }
        public JogRequestEventArgs(int motor, int direction) { Motor = motor; Direction = direction; }
    }
}
