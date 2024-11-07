#region Includes 
// System
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

// MonoGame
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Design;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Media;

// NobertEngine
using NobertEngine;
using NobertEngine.Core;
using NobertEngine.Core.Audio;
using NobertEngine.Core.Data;
using NobertEngine.Core.Game;
using NobertEngine.Core.Game.Events;
using NobertEngine.Core.Input;
using NobertEngine.Core.Debugging;
using NobertEngine.Core.Settings;
using NobertEngine.Entities;
using NobertEngine.Entities.Base;
using NobertEngine.Entities.Components;
using NobertEngine.Entities.Components.Physics;
using NobertEngine.Entities.Components.Stats;
using NobertEngine.Entities.Systems;
using NobertEngine.Entities.Systems.Draw;
using NobertEngine.Entities.Systems.Physics;
using NobertEngine.Entities.Systems.Stats;
using NobertEngine.Entities.Systems.AI;
using NobertEngine.Graphics;
using NobertEngine.Graphics.Animations;
using NobertEngine.Graphics.Rendering;
using NobertEngine.Graphics.UI;
using NobertEngine.Graphics.UI.Resources;
using NobertEngine.Graphics.UI.Elements;
using NobertEngine.Graphics.UI.HUD;
using NobertEngine.Inventory;
using NobertEngine.Inventory.Items;
using NobertEngine.Inventory.Management;
using NobertEngine.Networking;
using NobertEngine.Networking.Data;
using NobertEngine.Networking.PeerToPeer;
using NobertEngine.Networking.Client;
using NobertEngine.Networking.Messages;
using NobertEngine.Networking.Server;
using NobertEngine.Scenes;
using NobertEngine.Scenes.Creation;
using NobertEngine.Scenes.Cutscenes;
using NobertEngine.Scenes.Management;
using NobertEngine.Utilities;
using NobertEngine.Utilities.General;
using NobertEngine.Utilities.MathHelpers;
using NobertEngine.Utilities.Time;
#endregion

namespace NobertEngine
{
    namespace Networking
    {
        namespace Data
        {
            public abstract class GameData
            {
                public byte[] ToBytes()
                {
                    using (MemoryStream memoryStream = new MemoryStream())
                    using (BinaryWriter writer = new BinaryWriter(memoryStream))
                    {
                        SerializeData(writer);
                        return memoryStream.ToArray();
                    }
                }
                public static T FromBytes<T>(byte[] data) where T : GameData, new()
                {
                    var gameData = new T();

                    using (MemoryStream memoryStream = new MemoryStream(data))
                    using (BinaryReader reader = new BinaryReader(memoryStream))
                        gameData.DeserializeData(reader);

                    return gameData;
                }
                protected abstract void SerializeData(BinaryWriter writer);
                protected abstract void DeserializeData(BinaryReader reader);
            }

            public class PositionData : GameData
            {
                public Vector2 Position { get; set; }

                protected override void SerializeData(BinaryWriter writer)
                {
                    writer.Write(Position.X);
                    writer.Write(Position.Y);
                }
                protected override void DeserializeData(BinaryReader reader)
                {
                    Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                }
            }
        }
        namespace PeerToPeer
        {
            public class P2PManager
            {
                private TcpClient client;
                private TcpListener listener;
                private NetworkStream networkStream;
                private bool isHost;

                public bool IsConnected => client?.Connected ?? false;

                public P2PManager(bool host, int port)
                {
                    isHost = host;

                    if (isHost)
                    {
                        listener = new TcpListener(IPAddress.Any, port);
                        listener.Start();
                        Task.Run(() => AcceptConnectionAsync());
                    }
                    else
                        client = new TcpClient();
                }

                public async Task ConnectToHostAsync(string hostIp, int port)
                {
                    if (!isHost)
                    {
                        await client.ConnectAsync(IPAddress.Parse(hostIp), port);
                        networkStream = client.GetStream();
                        Console.WriteLine("Connected to the host");
                    }
                }
                private async Task AcceptConnectionAsync()
                {
                    client = await listener.AcceptTcpClientAsync();
                    networkStream = client.GetStream();
                    Console.WriteLine("Host connection established");
                }
                public async void SendData(byte[] data)
                {
                    if (networkStream == null)
                        return;

                    if (networkStream.CanWrite)
                        await networkStream.WriteAsync(data, 0, data.Length);
                }

                public async Task<byte[]> ReceiveData()
                {
                    if (networkStream == null)
                        return null;

                    if (networkStream.CanRead)
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                        return buffer[..bytesRead];
                    }
                    return null;
                }
                public void Close()
                {
                    networkStream?.Close();
                    client?.Close();
                    listener?.Stop();
                }
            }
        }
        namespace Client
        {
            public class ClientManager
            {
                private TcpClient tcpClient;
                private NetworkStream stream;

                public event Action<Message> OnMessageReceived;
               
                public async Task ConnectToServerAsync(string serverIp, int port)
                {
                    tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(serverIp, port);
                    stream = tcpClient.GetStream();
                    Console.WriteLine("Connected to the server");

                    ListenForServerUpdatesAsync();
                }
                public async void SendMessage(Message message)
                {
                    byte[] data = message.ToBytes();
                    await stream.WriteAsync(data, 0, data.Length);
                }
                private async void ListenForServerUpdatesAsync()
                {
                    byte[] buffer = new byte[1024];

                    while(tcpClient.Connected)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                        if (bytesRead > 0)
                        {
                            Message receivedMessage = Message.FromBytes(buffer[..bytesRead]);
                            OnMessageReceived?.Invoke(receivedMessage);
                        }
                    }
                }
                public void Close()
                {
                    stream?.Close();
                    tcpClient?.Close();
                }
            }
        }
        namespace Server
        {
            public class ServerManager
            {
                private TcpListener tcpListener;
                private List<TcpClient> clients = new List<TcpClient>();

                public event Action<TcpClient, Message> OnClientMessageReceived;

                public ServerManager(int port)
                {
                    tcpListener = new TcpListener(IPAddress.Any, port);
                    tcpListener.Start();
                    Console.WriteLine($"Server started on port: {port}");
                    AcceptClientsAsync();
                }
                private async void AcceptClientsAsync()
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    clients.Add(client);
                    Console.WriteLine($"Client connected");
                    ReceiveClientDataAsync(client);
                }
                private async void ReceiveClientDataAsync(TcpClient client)
                {
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[1024];

                    while (client.Connected)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                        if (bytesRead > 0)
                        {
                            Message receivedMessage = Message.FromBytes(buffer[..bytesRead]);
                            OnClientMessageReceived?.Invoke(client, receivedMessage);
                        }
                    }
                }
                public async void BroadcastMessage(Message message)
                {
                    byte[] data = message.ToBytes();

                    foreach (TcpClient client in clients)
                    {
                        if (client.Connected)
                            await client.GetStream().WriteAsync(data, 0, data.Length);
                    }
                }
                public void Stop()
                { 
                    tcpListener.Stop();
                    foreach (TcpClient client in clients)
                        client.Close();
                }
            }
        }
        namespace Messages
        {
            public abstract class Message
            {
                public virtual byte[] ToBytes()
                {
                    using (MemoryStream memoryStream = new MemoryStream())
                    using (BinaryWriter writer = new BinaryWriter(memoryStream))
                    {
                        SerializeData(writer);
                        return memoryStream.ToArray();
                    }
                }
                public static Message FromBytes(byte[] data)
                {
                    using (MemoryStream memoryStream = new MemoryStream())
                    using (BinaryReader reader = new BinaryReader(memoryStream))
                        return DeserializeData(reader);
                }
                protected abstract void SerializeData(BinaryWriter writer);
                protected static Message DeserializeData(BinaryReader reader)
                {
                    throw new NotImplementedException("Implement message type determination here");
                }
            }
            public class MovementMessage : Message
            {
                public Vector2 Position { get; private set; }

                public MovementMessage(Vector2 position)
                {
                    Position = position;
                }
                protected override void SerializeData(BinaryWriter writer)
                {
                    writer.Write(Position.X);
                    writer.Write(Position.Y);
                }
                protected static new Message DeserializeData(BinaryReader reader)
                {
                    float x = reader.ReadSingle();// Read then advance, similar to Pop() from Stacks
                    float y = reader.ReadSingle();
                    return new MovementMessage(new Vector2(x, y));
                }
            }
        }
    }
}
