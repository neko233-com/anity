using System;

namespace UnityEngine.Networking.PlayerConnection
{
    public delegate void MessageEventArgs(Guid messageId, byte[] data);

    public class PlayerConnection
    {
        private static PlayerConnection? _instance;

        public static PlayerConnection instance
        {
            get
            {
                _instance ??= new PlayerConnection();
                return _instance;
            }
        }

        public bool isConnected => false;

        public void Register(Guid messageId, MessageEventArgs callback) { }
        public void Unregister(Guid messageId, MessageEventArgs callback) { }
        public void Send(Guid messageId, byte[] data) { }
        public void Send(Guid messageId, byte[] data, int packageId) { }
        public void DisconnectAll() { }
        public void BlockUntilRecvMsg(int timeoutMs) { }
        public bool TrySend(Guid messageId, byte[] data) => false;
    }
}
