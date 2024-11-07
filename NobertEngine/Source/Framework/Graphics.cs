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
    namespace Graphics
    {
        namespace Rendering
        {
            public enum ScaleMode
            { Fit, Stretch }

            public class WindowScaler
            {
                private static WindowScaler instance;

                public int TargetWidth { get; private set; }
                public int TargetHeight { get; private set; }
                public ScaleMode ScaleMode { get; private set; }

                private GraphicsDeviceManager graphics;
                private Viewport viewport;

                private WindowScaler(GraphicsDeviceManager _graphics)
                {
                    graphics = _graphics;
                    LoadSettings();
                    ApplyScaling();
                }
                public static WindowScaler Initialize(GraphicsDeviceManager graphics)
                {
                    return instance ?? (instance = new WindowScaler(graphics));
                }
                private void LoadSettings()
                {
                    TargetWidth = int.Parse(ConfigManager.Instance.GetSetting("Graphics.TargetWidth", "1280"));
                    TargetHeight = int.Parse(ConfigManager.Instance.GetSetting("Graphics.TargetHeight", "720"));
                    ScaleMode = Enum.TryParse(ConfigManager.Instance.GetSetting("Graphics.ScaleMode", "Fit"), out ScaleMode mode) ? mode : ScaleMode.Fit;
                }

                public void ApplyScaling()
                {
                    float targetAspectRatio = (float)TargetWidth / TargetHeight;
                    float windowAspectRatio = (float)graphics.PreferredBackBufferWidth / graphics.PreferredBackBufferHeight;

                    switch (ScaleMode)
                    {
                        case ScaleMode.Fit:
                            ApplyFitScaling(targetAspectRatio, windowAspectRatio);
                            break;
                        case ScaleMode.Stretch:
                            ApplyStretchScaling();
                            break;
                        default:
                            ApplyFitScaling(targetAspectRatio, windowAspectRatio);
                            break;
                    }
                    graphics.ApplyChanges();
                }
                private void ApplyFitScaling(float targetAspectRatio, float windowAspectRatio)
                {
                    if (windowAspectRatio > targetAspectRatio)
                    {
                        int width = graphics.PreferredBackBufferHeight * (int)targetAspectRatio;
                        viewport = new Viewport((graphics.PreferredBackBufferWidth - width) / 2, 0, width, graphics.PreferredBackBufferHeight);
                    }
                    else
                    {
                        int height = graphics.PreferredBackBufferWidth / (int)targetAspectRatio;
                        viewport = new Viewport(0,(graphics.PreferredBackBufferHeight - height) / 2, graphics.PreferredBackBufferHeight, height);
                    }
                    graphics.GraphicsDevice.Viewport = viewport;
                }
                private void ApplyStretchScaling()
                {
                    viewport = new Viewport(0,0,graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight);
                    graphics.GraphicsDevice.Viewport = viewport;
                }
                public void UpdateWindowSize(int width, int height)
                {
                    graphics.PreferredBackBufferWidth = width;
                    graphics.PreferredBackBufferHeight = height;
                    ApplyScaling();
                }
                public Matrix GetScaleMatrix()
                {
                    float scaleX = (float)viewport.Width / TargetWidth;
                    float scaleY = (float)viewport.Height / TargetHeight;
                    return Matrix.CreateScale(scaleX, scaleY, 1.0f);
                }
            }
        }
        namespace Animations
        {
            public class AnimationFrame
            {
                public Rectangle SourceRectangle { get; private set; }
                public float Duration { get; private set; }

                public AnimationFrame(Rectangle sourceRectangle, float duration)
                {
                    SourceRectangle = sourceRectangle;
                    Duration = duration;
                }
            }
            public class Animation
            { 
                private List<AnimationFrame> frames = new List<AnimationFrame>();
                public Texture2D Texture { get; private set; }
                public bool IsLooping { get; set; }
                public float TotalDuration { get; private set; }

                public Animation(Texture2D texture, bool isLooping)
                {
                    Texture = texture;
                    IsLooping = isLooping;
                    TotalDuration = 0;
                }
                public void AddFrame(AnimationFrame frame)
                {
                    frames.Add(frame);
                    TotalDuration += frame.Duration;
                }
                public AnimationFrame GetFrame(float elapsedTime)
                {
                    if (frames.Count == 0)
                        return null;

                    if (IsLooping)
                        elapsedTime %= TotalDuration;
                    else if (elapsedTime >= TotalDuration)
                        elapsedTime = TotalDuration - 0.01f;// Clamp to last frame

                    float accumulatedTime = 0;
                    foreach (AnimationFrame frame in frames)
                    {
                        accumulatedTime += frame.Duration;
                        if (elapsedTime < accumulatedTime)
                            return frame;
                    }
                    return frames[^1];// Returns the last frame as a fallback
                }
            }
            public class Animator
            {
                private Dictionary<string, Animation> animations = new Dictionary<string, Animation>();
                private Animation currentAnimation;
                private float currentTime;
                private string previousAnimationKey, currentAnimationKey;

                public void AddAnimation(string key, Animation animation)
                {
                    if (animations.ContainsKey(key) == false)
                        animations[key] = animation;
                }
                public void RemoveAnimation(string key)
                {
                    if (animations.ContainsKey(key))
                        animations.Remove(key);
                }
                public bool HasAnimation(string key)
                {
                    return animations.ContainsKey(key);
                }
                public void PlayAnimation(string key)
                {
                    if (currentAnimationKey != key && animations.ContainsKey(key))
                    {
                        previousAnimationKey = currentAnimationKey;
                        currentAnimationKey = key;
                        currentAnimation = animations[key];
                        currentTime = 0; // Resets animation
                    }
                }
                public void CancelAnimation()
                {
                    currentTime = animations[currentAnimationKey].TotalDuration;
                    PlayAnimation(previousAnimationKey);
                }
                public void CancelAnimation(string newAnimationKey)
                {
                    currentTime = animations[currentAnimationKey].TotalDuration;
                    PlayAnimation(newAnimationKey);
                }
                public void Update(float deltaTime)
                {
                    if (currentAnimation != null)
                        currentTime += deltaTime;
                }
                public void Draw(SpriteBatch spriteBatch, Vector2 position, float rotation, Vector2 origin, float scale = 1.0f)
                {
                    if (currentAnimation != null)
                    {
                        AnimationFrame frame = currentAnimation.GetFrame(currentTime);
                        if (frame != null)
                        {
                            spriteBatch.Draw(
                                currentAnimation.Texture,
                                position,
                                frame.SourceRectangle,
                                Color.White,
                                rotation,
                                origin,
                                scale,
                                SpriteEffects.None,
                                0f);
                        }
                    }
                }
            }
        }
        namespace UI
        {
            namespace Resources
            {
                public static class UIResource
                {
                    public static Texture2D WhiteTexture { get; private set; }

                    public static void Initialize(GraphicsDevice graphicsDevice)
                    {
                        WhiteTexture = new Texture2D(graphicsDevice, 1, 1);
                        WhiteTexture.SetData(new[] { Color.White });
                    }
                }
            }
            namespace Elements
            {
                public abstract class Element
                {
                    public Color Color { get; set; } = Color.White;
                    public virtual void Update(GameTime gameTime) { }
                    public virtual void Update(MouseState mouseState) { }
                    public virtual void Draw(SpriteBatch spriteBatch, SpriteFont spriteFont = null) { }
                    public virtual void Draw(SpriteBatch spriteBatch, Texture2D texture) { }
                }
                public class Button : Element
                {
                    public Rectangle Bounds { get; set; }
                    public string Text { get; set; }
                    public bool IsHovered { get; private set; }
                    public bool IsClicked { get; private set; }

                    public event Action OnClick;

                    private MouseState lastMouseState;

                    public Button(Rectangle bounds, string text)
                    {
                        Bounds = bounds;
                        Text = text;
                    }
                    public override void Update(MouseState mouseState)
                    {
                        base.Update(mouseState);

                        IsHovered = Bounds.Contains(mouseState.Position);
                        IsClicked = IsHovered && mouseState.LeftButton == ButtonState.Pressed;

                        if (IsClicked && mouseState.LeftButton == ButtonState.Released)
                            OnClick?.Invoke();

                        lastMouseState = mouseState;
                    }
                    public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
                    {
                        base.Draw(spriteBatch, font);

                        Color drawColor = IsHovered ? Color.Yellow : Color;
                        spriteBatch.Draw(UIResource.WhiteTexture, Bounds, drawColor);

                        Vector2 textSize = font.MeasureString(Text);
                        Vector2 textPosition = new Vector2(Bounds.Center.X, Bounds.Center.Y) - textSize / 2;
                        spriteBatch.DrawString(font, Text, textPosition, Color.Black);
                    }
                }
                public class Label : Element
                {
                    public Vector2 Position { get; set; }
                    public string Text { get; set; }

                    public Label(Vector2 position, string text)
                    {
                        Position = position;
                        Text = text;
                    }
                    public override void Update(GameTime gameTime)
                    {
                        base.Update(gameTime);
                    }
                    public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
                    {
                        base.Draw(spriteBatch, font);

                        spriteBatch.DrawString(font, Text, Position, Color);
                    }
                }
                public class Slider : Element
                {
                    public Rectangle Bounds { get; set; }
                    public float Value { get; private set; }

                    public Slider(Rectangle bounds, float initialValue = 0.5f)
                    {
                        Bounds = bounds;
                        Value = MathHelper.Clamp(initialValue, 0f, 1f);
                    }

                    public override void Update(MouseState mouseState)
                    {
                        base.Update(mouseState);

                        if (mouseState.LeftButton == ButtonState.Pressed && Bounds.Contains(mouseState.Position))
                        {
                            Value = (mouseState.X - Bounds.X) / (float)Bounds.Width;
                            Value = MathHelper.Clamp(Value, 0f, 1f);
                        }
                    }
                    public override void Draw(SpriteBatch spriteBatch, Texture2D texture)
                    {
                        base.Draw(spriteBatch, texture);

                        //Background
                        spriteBatch.Draw(texture, Bounds, Color.Gray);
                        //Fill
                        int fillWidth = Bounds.Width * (int)Value;
                        spriteBatch.Draw(texture, new Rectangle(Bounds.X, Bounds.Y, fillWidth, Bounds.Height), Color.Gray);
                    }
                }
            }
            namespace HUD
            {
                public class Bar
                {
                    public Rectangle Bounds { get; set; }
                    public Color FullColor { get; private set; }
                    public Color HalfColor { get; private set; }
                    public Color LowColor { get; private set; }
                    public float Value { get; private set; }

                    public Bar(Rectangle bounds, Color full, Color half, Color low, float initial = 1f)
                    {
                        Bounds = bounds;
                        FullColor = full;
                        HalfColor = half;
                        LowColor = low;
                        Value = MathHelper.Clamp(initial, 0f, 1f);

                    }
                    public void Update(float health)
                    {
                        Value = MathHelper.Clamp(health, 0f, 1f);
                    }
                    public void Draw(SpriteBatch spriteBatch, Texture2D texture)
                    {
                        spriteBatch.Draw(texture, Bounds, Color.Gray);

                        int fillWidth = Bounds.Width * (int)Value;
                        spriteBatch.Draw(texture, new Rectangle(Bounds.X, Bounds.Y, fillWidth, Bounds.Height), GetBarColor(Value/1f));
                    }
                    public Color GetBarColor(float value)
                    {
                        if (value > 0.5f)
                            return FullColor;
                        if (value > 0.25f)
                           return HalfColor;

                        return LowColor;
                    }
                }
            }
        }
    }
}
