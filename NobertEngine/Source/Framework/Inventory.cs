#region Includes 
// System
using System;
using System.IO;
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
    namespace Inventory
    {
        namespace Items
        {
            public class ItemData
            {
                public int ID { get; set; }
                public string Name { get; set; }
                public string Description { get; set; }
                public int Value { get; set; }
                public string ItemType { get; set; }
                public Texture2D Texture { get; set; }
            }
            public class Item
            {
                public string Name { get; private set; }
                public int ID { get; private set; }
                public string Description { get; private set; }
                public int Value { get; private set; }
                public Texture2D Texture { get; private set; }
                public Item(int id, string name, string description, int value, Texture2D texture)
                {
                    ID = id;
                    Name = name;
                    Description = description;
                    Value = value;
                    Texture = texture;
                }
                public virtual ItemData ToItemData()
                {
                    return new ItemData
                    {
                        ID = ID,
                        Name = Name,
                        Description = Description,
                        Value = Value,
                        ItemType = this.GetType().Name
                    };
                }
                public static Item FromItemData(ItemData itemData)
                {
                    switch (itemData.ItemType)
                    {
                        case "ConsumableItem":// Use of Example class
                            return new ConsumableItem(itemData.ID, itemData.Name, itemData.Description, itemData.Value, itemData.Texture);
                        default:
                            return new Item(itemData.ID, itemData.Name, itemData.Description, itemData.Value, itemData.Texture);
                    }
                }
                public void Draw(SpriteBatch spriteBatch, Vector2 position, int width, int height)
                {
                    Rectangle destinationRect = new Rectangle((int)position.X, (int)position.Y, width, height);
                    spriteBatch.Draw(Texture, destinationRect, Color.White);
                }
            }
            public class ConsumableItem : Item// Example class
            {
                public ConsumableItem(int id, string name, string description, int value, Texture2D texture) : base(id, name, description, value, texture)
                {
                }
            }
            public class InventoryWindow
            {
                public Rectangle Bounds { get; set; }
                public List<Item> items = new List<Item>();
                private int columns;
                private int rows;
                private int cellSize;
                private int padding = 5;

                public InventoryWindow(Rectangle bounds, int _columns, int _rows, int _cellSize)
                {
                    Bounds = bounds;

                    columns = _columns;
                    rows = _rows;
                    cellSize = _cellSize;
                }
                public void AddItem(Item item)
                {
                    items.Add(item);
                }
                public void RemoveItem(Item item)
                {
                    if (items.Contains(item) == false)
                        return;

                    items.Remove(item);
                }
                public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D backgroundTexture)
                {
                    spriteBatch.Draw(backgroundTexture, Bounds, Color.White);

                    Vector2 startPosition = new Vector2(Bounds.X + padding, Bounds.Y + padding);

                    for (int i = 0; i < items.Count; i++)
                    {
                        int column = i % columns;
                        int row = i / columns;

                        if (row >= rows)
                            break;

                        Vector2 position = startPosition + new Vector2(column * (cellSize * padding), rows * (cellSize * padding));
                        items[i].Draw(spriteBatch, position, cellSize, cellSize);
                    }
                }
            }
        }
        namespace Management
        {
            public class InventoryData
            {
                public List<ItemData> Items { get; set; } = new List<ItemData>();
            }
            public class Inventory
            {
                private List<Item> items;

                public Inventory()
                {
                    items = new List<Item>();
                }
                public void AddItem(Item item)
                {
                    items.Add(item);
                }
                public void RemoveItem(Item item)
                {
                    items.Remove(item);
                }
                public InventoryData ToInventoryData()
                {
                    var inventoryData = new InventoryData();
                    foreach(Item item in items)
                        inventoryData.Items.Add(item.ToItemData());
                    return inventoryData;
                }
                public void LoadFromInventoryData(InventoryData inventoryData)
                {
                    items.Clear();
                    foreach (ItemData itemData in inventoryData.Items)
                        items.Add(Item.FromItemData(itemData));
                }
            }
            public class TradeOffer
            {
                public string Trader { get; private set; }
                public List<Item> Items { get; private set; } = new List<Item>();

                public TradeOffer(string trader)
                {
                    Trader = trader;
                }
                public void AddItem(Item item)
                {
                    Items.Add(item);
                }
                public void RemoveItem(Item item)
                {
                    Items.Remove(item);
                }
                public void ClearOffer()
                {
                    Items.Clear();
                }
            }
            public class Trade
            {
                public TradeOffer OfferA { get; private set; }
                public TradeOffer OfferB { get; private set; }
                public bool IsAccepted { get; private set; }

                public Trade(string traderA, string traderB)
                {
                    OfferA = new TradeOffer(traderA);
                    OfferB = new TradeOffer(traderB);
                }
                public bool IsTradeAccepted(Inventory inventoryA, Inventory inventoryB)
                {
                    if (IsAccepted)
                        return false;// Prevent re-accepting trade

                    foreach (Item item in OfferA.Items)
                    {
                        inventoryA.RemoveItem(item);
                        inventoryB.AddItem(item);
                    }
                    foreach (Item item in OfferB.Items)
                    {
                        inventoryB.RemoveItem(item);
                        inventoryA.AddItem(item);
                    }

                    OfferA.ClearOffer();
                    OfferB.ClearOffer();

                    IsAccepted = true;
                    return IsAccepted;
                }
                public void CancelTrade()
                {
                    OfferA.ClearOffer();
                    OfferB.ClearOffer();
                    IsAccepted = false;
                }
            }
            public class TradeManager
            {
                private List<Trade> activeTrades = new List<Trade>();

                public Trade StartTrade(string traderA, Inventory inventoryA, string traderB, Inventory inventoryB)
                {
                    Trade newTrade = new Trade(traderA, traderB);
                    activeTrades.Add(newTrade);

                    return newTrade;
                }
                public bool AcceptTrade(Trade trade, Inventory inventoryA, Inventory inventoryB)
                {
                    if (activeTrades.Contains(trade) && trade.IsTradeAccepted(inventoryA, inventoryB))
                    {
                        activeTrades.Remove(trade);
                        return true;
                    }
                    return false;
                }
                public bool CancelTrade(Trade trade)
                {
                    if (activeTrades.Contains(trade))
                    {
                        trade.CancelTrade();
                        activeTrades.Remove(trade);
                        return true;
                    }
                    return false;
                }
                public List<Trade> GetActiveTrades()
                {
                    return new List<Trade>(activeTrades);
                }
            }
        }
    }
}
