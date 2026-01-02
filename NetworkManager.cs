// ################################################
// FILE: NetworkManager.cs
// ################################################
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace GameEngine
{
    public enum NetworkMessageType
    {
        PlayerConnect,
        PlayerDisconnect,
        PlayerPosition,
        PlayerState,
        LevelChange,
        EntityUpdate
    }

    public class NetworkMessage
    {
        public NetworkMessageType Type { get; set; }
        public string SenderId { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public DateTime Timestamp { get; set; }

        public NetworkMessage()
        {
            Data = new Dictionary<string, object>();
            Timestamp = DateTime.Now;
        }
    }

    public class NetworkManager
    {
        public bool IsConnected { get; private set; }
        public int PlayerCount { get; private set; }
        public Dictionary<string, object> ConnectedPlayers { get; private set; }

        public event Action<NetworkMessage> OnMessageReceived;
        public event Action<string> OnPlayerConnected;
        public event Action<string> OnPlayerDisconnected;

        public NetworkManager()
        {
            ConnectedPlayers = new Dictionary<string, object>();
            IsConnected = false;
            PlayerCount = 1; // Local player
        }

        public void Initialize()
        {
            // Initialize network connection
            IsConnected = false;
        }

        public void Update(float deltaTime)
        {
            // Update network state
            if (!IsConnected)
            {
                AttemptConnection();
            }
        }

        private void AttemptConnection()
        {
            // Simulate connection attempt
            // In a real implementation, this would connect to a server
        }

        public void Send(NetworkMessage message)
        {
            // Send message over network
            // In a real implementation, this would send via network protocol
        }

        public void ReceiveMessage(NetworkMessage message)
        {
            OnMessageReceived?.Invoke(message);
        }

        public void ConnectToServer(string serverAddress, int port)
        {
            // Connect to network server
            IsConnected = true;
            PlayerCount = 1; // At least the local player
        }

        public void Disconnect()
        {
            IsConnected = false;
            ConnectedPlayers.Clear();
            PlayerCount = 0;
        }

        public void SetLocalPlayerState(Vector2 position, int health, int ammo, bool isAlive)
        {
            if (!IsConnected) return;

            var message = new NetworkMessage
            {
                Type = NetworkMessageType.PlayerState,
                SenderId = "local_player",
                Data = new Dictionary<string, object>
                {
                    ["position"] = $"{position.X},{position.Y}",
                    ["health"] = health,
                    ["ammo"] = ammo,
                    ["isAlive"] = isAlive
                }
            };

            Send(message);
        }

        public object GetPlayerById(string playerId)
        {
            if (ConnectedPlayers.ContainsKey(playerId))
                return ConnectedPlayers[playerId];
            
            return null;
        }
    }
}