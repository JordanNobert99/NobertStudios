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
    namespace Core
    {
        namespace Game
        {
            public enum GameState
            { MainMenu, Running, Paused, GameOver }

            public class GameManager
            {
                private static GameManager instance;
                public GameState CurrentState { get; private set; }

                public int InitialScore { get; private set; }
                public float VolumeLevel { get; private set; }

                public static GameManager Instance => instance ?? (instance = new GameManager());
                private GameManager()
                {
                    LoadSettings();
                    CurrentState = GameState.MainMenu;
                }
                private void LoadSettings()
                {
                    InitialScore = int.Parse(ConfigManager.Instance.GetSetting("Game.InitialScore", "0"));
                    VolumeLevel = float.Parse(ConfigManager.Instance.GetSetting("Audio.VolumeLevel", "0.5"));
                }
                public void ChangeState(GameState newState)
                {
                    CurrentState = newState;
                }
                public void Update(GameTime gameTime)
                {
                    switch (CurrentState)
                    {
                        case GameState.MainMenu:
                            break;
                        case GameState.Running:
                            break;
                        case GameState.Paused:
                            break;
                        case GameState.GameOver:
                            break;
                        default:
                            break;
                    }
                }
                public void StartGame()
                {
                    ChangeState(GameState.Running);
                }
                public void PauseGame()
                {
                    ChangeState(GameState.Paused);
                }
                public void ResumeGame()
                {
                    if (CurrentState != GameState.Paused)
                        return;

                    ChangeState(GameState.Running);
                }
                public void EndGame()
                {
                    ChangeState(GameState.GameOver);
                }
                public void ReturnToMainMenu()
                {
                    ChangeState(GameState.MainMenu);
                }
            }

            namespace Events
            {
                public abstract class GameEvent
                {
                    public DateTime EventTime { get; private set; }

                    protected GameEvent()
                    {
                        EventTime = DateTime.Now;
                    }
                }
                public class EventManager
                {
                    private readonly Dictionary<Type, List<Delegate>> eventHandlers = new Dictionary<Type, List<Delegate>>();

                    public void Subscribe<T>(Action<T> handler) where T : GameEvent
                    {
                        var eventType = typeof(T);

                        if (!eventHandlers.ContainsKey(eventType))
                            eventHandlers[eventType] = new List<Delegate>();

                        eventHandlers[eventType].Add(handler);
                    }
                    public void Unsubscribe<T>(Action<T> handler) where T : GameEvent
                    {
                        var eventType = typeof(T);

                        if (eventHandlers.ContainsKey(eventType))
                            eventHandlers[eventType].Remove(handler);
                    }
                    public void Trigger<T>(T gameEvent) where T : GameEvent
                    {
                        var eventType = typeof(T);

                        if (eventHandlers.ContainsKey(eventType))
                        {
                            foreach (var handler in eventHandlers[eventType])
                                ((Action<T>)handler)?.Invoke(gameEvent);
                        }
                    }
                }
            }
        }
        namespace Input
        {
            public class KeyBindings
            {
                public Dictionary<string, Func<MouseState, bool>> MouseBindings { get; set; }
                public Dictionary<string, Keys> KeyboardBindings { get; set; }
                public Dictionary<string, Buttons> GamepadBindings { get; set; }
                public Dictionary<string, Rectangle> TouchBindings { get; set; }
                public KeyBindings()
                {
                    MouseBindings = new Dictionary<string, Func<MouseState, bool>>();
                    KeyboardBindings = new Dictionary<string, Keys>();
                    GamepadBindings = new Dictionary<string, Buttons>();
                    TouchBindings = new Dictionary<string, Rectangle>();
                }
                public void BindMouseAction(string action, Func<MouseState, bool> condition)
                {
                    MouseBindings[action] = condition;
                }
                public void BindKeyboardAction(string action, Keys key)
                {
                    KeyboardBindings[action] = key;
                }
                public void BindGamepadAction(string action, Buttons button)
                {
                    GamepadBindings[action] = button;
                }
                public void BindTouchAction(string action, Rectangle touchArea)
                {
                    TouchBindings[action] = touchArea;
                }
            }
            public abstract class InputHandler
            {
                public KeyBindings keyBindings { get; private set; }
                public Dictionary<string, bool> previousActionStates { get; private set; }
                public InputHandler(KeyBindings bindings)
                {
                    keyBindings = bindings;
                    previousActionStates = new Dictionary<string, bool>();
                }
                public virtual void Update()
                {

                }
                public virtual bool IsActionPressed(string action)
                {
                    return false;
                }
                public virtual bool IsActionHeld(string action)
                {
                    return false;
                }
                public virtual bool IsActive()
                {
                    throw new NotImplementedException();
                }
            }
            public class MouseInput : InputHandler
            {
                private int previousScrollValue;
                private MouseState previousMouseState;

                public MouseInput(KeyBindings bindings) : base(bindings)
                {
                    previousMouseState = Mouse.GetState();
                    previousScrollValue = previousMouseState.ScrollWheelValue;
                }
                public int ScrollDelta { get; private set; }
                public override void Update()
                {
                    base.Update();

                    var currentMouseState = Mouse.GetState();
                    ScrollDelta = currentMouseState.ScrollWheelValue - previousScrollValue;
                    previousScrollValue = currentMouseState.ScrollWheelValue;

                    foreach (string action in keyBindings.MouseBindings.Keys)
                        previousActionStates[action] = IsActionPressed(action, previousMouseState);
                }
                private bool IsActionPressed(string action, MouseState mouseState)
                {
                    if (keyBindings.MouseBindings.TryGetValue(action, out Func<MouseState, bool> condition))
                        return condition(mouseState);
                    return base.IsActionPressed(action);
                }
                public override bool IsActionPressed(string action)
                {
                    return IsActionPressed(action, Mouse.GetState());
                }
                public override bool IsActionHeld(string action)
                {
                    if (previousActionStates.TryGetValue(action, out bool wasHeld))
                        return wasHeld && IsActionPressed(action);
                    return base.IsActionPressed(action);
                }
                public Vector2 GetMousePosition()
                {
                    var mouseState = Mouse.GetState();
                    return new Vector2(mouseState.X, mouseState.Y);
                }
                public Vector2 GetMouseDirection()
                {
                    Point previousMousePosition = previousMouseState.Position;
                    return GetMousePosition() - new Vector2(previousMousePosition.X, previousMousePosition.Y);
                }
                public override bool IsActive()
                {
                    var mouseState = Mouse.GetState();
                    return mouseState.LeftButton == ButtonState.Pressed ||
                        mouseState.RightButton == ButtonState.Pressed ||
                        mouseState.MiddleButton == ButtonState.Pressed ||
                        ScrollDelta != 0;
                }
            }
            public class KeyboardInput : InputHandler
            {
                public KeyboardInput(KeyBindings bindings) : base(bindings)
                {

                }
                public override void Update()
                {
                    base.Update();

                    foreach (string action in keyBindings.KeyboardBindings.Keys)
                        previousActionStates[action] = IsActionPressed(action);
                }
                public bool IsActionPressed(Keys key)
                {
                    return Keyboard.GetState().IsKeyDown(key);
                }
                public override bool IsActionPressed(string action)
                {
                    if (keyBindings.KeyboardBindings.TryGetValue(action, out Keys key))
                        return Keyboard.GetState().IsKeyDown(key);
                    return base.IsActionPressed(action);
                }
                public override bool IsActionHeld(string action)
                {
                    if (previousActionStates.TryGetValue(action, out bool wasHeld))
                        return wasHeld && IsActionPressed(action);
                    return base.IsActionHeld(action);
                }
                public Vector2 GetArrowDirection()
                {
                    Vector2 direction = Vector2.Zero;
                    if (IsActionPressed(Keys.Up)) direction.Y -= 1;
                    if (IsActionPressed(Keys.Down)) direction.Y += 1;
                    if (IsActionPressed(Keys.Left)) direction.X -= 1;
                    if (IsActionPressed(Keys.Right)) direction.X += 1;
                    return direction;
                }
                public override bool IsActive()
                {
                    return Keyboard.GetState().GetPressedKeys().Length > 0;
                }
            }
            public class GamepadInput : InputHandler
            {
                private PlayerIndex playerIndex;
                public GamepadInput(KeyBindings bindings, PlayerIndex _playerIndex) : base(bindings)
                {
                    playerIndex = _playerIndex;
                }
                public override void Update()
                {
                    base.Update();

                    foreach (string action in keyBindings.GamepadBindings.Keys)
                        previousActionStates[action] = IsActionPressed(action);
                }
                public override bool IsActionPressed(string action)
                {
                    if (keyBindings.GamepadBindings.TryGetValue(action, out Buttons button))
                        return GamePad.GetState(playerIndex).IsButtonDown(button);
                    return base.IsActionPressed(action);
                }
                public override bool IsActionHeld(string action)
                {
                    if (previousActionStates.TryGetValue(action, out bool wasHeld))
                        return wasHeld && IsActionPressed(action);
                    return base.IsActionHeld(action);
                }
                public Vector2 GetLeftThumbstickDirection()
                {
                    return GamePad.GetState(playerIndex).ThumbSticks.Left;
                }
                public Vector2 GetRightThumbstickDirection()
                {
                    return GamePad.GetState(playerIndex).ThumbSticks.Right;
                }
                public override bool IsActive()
                {
                    return GamePad.GetState(playerIndex).IsConnected;
                }
            }
            public class TouchInput : InputHandler
            {
                private Vector2 joystickCenter;
                private Vector2 joystickPosition;
                private float joystickRadius;
                private bool joystickIsActive;

                public TouchInput(KeyBindings bindings, Vector2 _joystickCenter, float _joystickRadius) : base(bindings)
                {
                    joystickCenter = _joystickCenter;
                    joystickRadius = _joystickRadius;
                    joystickPosition = joystickCenter;
                }
                public override void Update()
                {
                    base.Update();

                    var touchState = TouchPanel.GetState();

                    joystickIsActive = false;
                    joystickPosition = joystickCenter;

                    foreach (string action in keyBindings.TouchBindings.Keys)
                    {
                        bool isPressed = false;
                        foreach (TouchLocation touch in touchState)
                        {
                            if (touch.State == TouchLocationState.Pressed || touch.State == TouchLocationState.Moved)
                            {
                                if (keyBindings.TouchBindings[action].Contains(touch.Position))
                                    isPressed = true;
                            }
                        }
                        previousActionStates[action] = isPressed;
                    }

                    foreach (TouchLocation touch in touchState)
                    {
                        if (Vector2.Distance(touch.Position, joystickCenter) <= joystickRadius)
                        {
                            joystickIsActive = true;
                            joystickPosition = touch.Position;
                            break;
                        }
                    }
                }
                public override bool IsActionPressed(string action)
                {
                    if (keyBindings.TouchBindings.TryGetValue(action, out Rectangle touchArea))
                    {
                        var touchState = TouchPanel.GetState();
                        foreach (TouchLocation touch in touchState)
                        {
                            if (touchArea.Contains(touch.Position))
                                return true;
                        }
                    }
                    return base.IsActionPressed(action);
                }
                public override bool IsActionHeld(string action)
                {
                    if (previousActionStates.TryGetValue(action, out bool wasHeld))
                        return wasHeld && IsActionPressed(action);
                    return base.IsActionHeld(action);
                }
                public Vector2 GetJoystickDirection()
                {
                    if (joystickIsActive)
                        return Vector2.Normalize(joystickPosition - joystickCenter);
                    return Vector2.Zero;
                }
                public override bool IsActive()
                {
                    return TouchPanel.GetState().Count > 0;
                }
            }
            public class InputManager
            {
                private InputHandler activeInputHandler;
                private KeyBindings keyBindings;

                public InputManager(KeyBindings bindings, InputHandler inputHandler)
                {
                    keyBindings = bindings;

                    if (inputHandler is MouseInput mouseInput)
                        activeInputHandler = mouseInput;
                    else if (inputHandler is KeyboardInput keyboardInput)
                        activeInputHandler = keyboardInput;
                    else if (inputHandler is GamepadInput gamepadInput)
                        activeInputHandler = gamepadInput;
                    else if (inputHandler is TouchInput touchInput)
                        activeInputHandler = touchInput;
                }
                public void Update()
                {
                    activeInputHandler.Update();
                }

                public bool IsActionPressed(string action)
                {
                    return activeInputHandler.IsActionPressed(action);
                }
                public bool IsActionHeld(string action)
                {
                    return activeInputHandler.IsActionHeld(action);
                }
                public Vector2 GetMovementDirection()
                {
                    Vector2 direction = Vector2.Zero;
                    if (activeInputHandler is MouseInput mouseInput)
                        direction = mouseInput.GetMouseDirection();
                    else if (activeInputHandler is KeyboardInput keyboardInput)
                        direction = keyboardInput.GetArrowDirection();
                    else if (activeInputHandler is GamepadInput gamepadInput)
                        direction = gamepadInput.GetLeftThumbstickDirection();
                    else if (activeInputHandler is TouchInput touchInput)
                        direction = touchInput.GetJoystickDirection();
                    return direction;
                }
                public Vector2 GetAimDirection()
                {
                    Vector2 direction = Vector2.Zero;

                    if (activeInputHandler is MouseInput mouseInput)
                        direction = mouseInput.GetMouseDirection();
                    else if (activeInputHandler is KeyboardInput keyboardInput)
                        direction = keyboardInput.GetArrowDirection();
                    else if (activeInputHandler is GamepadInput gamepadInput)
                        direction = gamepadInput.GetRightThumbstickDirection();
                    else if (activeInputHandler is TouchInput touchInput)
                        direction = touchInput.GetJoystickDirection();

                    return direction;
                }
                public int GetScrollDelta()
                {
                    int delta = 0;

                    if (activeInputHandler is MouseInput mouseInput)
                        delta = mouseInput.ScrollDelta;
                    else if (activeInputHandler is KeyboardInput keyboardInput)
                    {
                        int pos = keyboardInput.IsActionPressed(Keys.Up) || keyboardInput.IsActionPressed(Keys.Right) ? 1 : 0;
                        int neg = keyboardInput.IsActionPressed(Keys.Down) || keyboardInput.IsActionPressed(Keys.Left) ? -1 : 0;
                        delta = pos + neg;
                    }
                    else if (activeInputHandler is GamepadInput gamepadInput)
                    {
                        int pos = gamepadInput.GetLeftThumbstickDirection().X == 1 || gamepadInput.GetRightThumbstickDirection().X == 1
                            || gamepadInput.GetLeftThumbstickDirection().Y == 1 || gamepadInput.GetRightThumbstickDirection().Y == 1 ? 1 : 0;
                        int neg = gamepadInput.GetLeftThumbstickDirection().X == -1 || gamepadInput.GetRightThumbstickDirection().X == -1
                            || gamepadInput.GetLeftThumbstickDirection().Y == -1 || gamepadInput.GetRightThumbstickDirection().Y == -1 ? -1 : 0;
                        delta = pos + neg;
                    }
                    else if (activeInputHandler is TouchInput touchInput)
                        delta = (int)Math.Max(touchInput.GetJoystickDirection().X, touchInput.GetJoystickDirection().Y);

                    return delta;
                }
                public bool IsActive()
                {
                    return activeInputHandler.IsActive();
                }
            }
        }
        namespace Settings
        {
            public class ConfigManager
            {
                private static ConfigManager instance;
                private readonly string configFilePath;
                private static string defaultFilePath = "";
                private Dictionary<string, string> settings;

                public static ConfigManager Instance => instance ?? (instance = new ConfigManager(defaultFilePath));
                public ConfigManager(string _configFilePath)
                {
                    configFilePath = _configFilePath;
                    defaultFilePath = configFilePath;
                    LoadConfig();
                }
                public void LoadConfig()
                {
                    settings = SerializationManager.LoadFromFile<Dictionary<string, string>>(configFilePath) ?? new Dictionary<string, string>();
                }
                public void SaveConfig()
                {
                    SerializationManager.SaveToFile(configFilePath, settings);
                }
                public string GetSetting(string key, string defaultValue = null)
                {
                    return settings.ContainsKey(key) ? settings[key] : defaultValue;
                }
                public void SetSetting(string key, string value)
                {
                    settings[key] = value;
                }
            }
        }
        namespace Audio
        {
            public class PooledSoundInstance
            {
                public string ClipName { get; }
                public SoundEffectInstance Instance { get; }
                public PooledSoundInstance(string clipName, SoundEffectInstance instance)
                {
                    ClipName = clipName;
                    Instance = instance;
                }
            }
            public class MusicManager
            {
                private float volume = 1.0f;
                private Dictionary<string, Song> songs = new Dictionary<string, Song>();
                private Song? currentSong;
                private float fadeTargetVolume;
                private float fadeDuration;
                private float fadeElapsedTime;
                private bool isFading;
                public float Volume
                {
                    get { return volume; }
                    set
                    {
                        volume = value;
                        MediaPlayer.Volume = volume;
                    }
                }
                public void LoadSong(string name, Song song)
                {
                    songs[name] = song;
                }
                public void Stop()
                {
                    if (currentSong != null)
                    {
                        MediaPlayer.Stop();
                        currentSong = null;
                    }
                }
                public void Play(string name, bool loop = true)
                {
                    Stop();
                    currentSong = songs[name];
                    MediaPlayer.IsRepeating = loop;
                    MediaPlayer.Volume = volume;
                    MediaPlayer.Play(currentSong);
                }
                public void StopFadeOut(float duration)
                {
                    fadeTargetVolume = 0f;
                    fadeDuration = duration;
                    fadeElapsedTime = 0f;
                    isFading = true;
                }
                public void PlayFadeIn(string name, float targetVolume, float duration, bool loop = true)
                {
                    Play(name, loop);
                    volume = 0;
                    MediaPlayer.Volume = volume;
                    fadeTargetVolume = targetVolume;
                    fadeDuration = duration;
                    fadeElapsedTime = 0f;
                    isFading = true;
                }
                public void Update(float deltaTime)
                {
                    if (!isFading) return;

                    fadeElapsedTime += deltaTime;
                    float progress = Math.Min(fadeElapsedTime / fadeDuration, 1.0f);
                    volume = MathHelper.Lerp(volume, fadeTargetVolume, progress);
                    MediaPlayer.Volume = volume;

                    if (progress >= 1.0f)
                        isFading = false;
                }
                public void Dispose()
                {
                    MediaPlayer.Stop();
                    songs.Clear();
                }
            }
            public class SFXManager
            {
                private float volume = 1.0f;
                private Dictionary<string, SoundEffect> soundEffects = new Dictionary<string, SoundEffect>();
                private Dictionary<string, Stack<PooledSoundInstance>> instancePool = new Dictionary<string, Stack<PooledSoundInstance>>();
                private List<PooledSoundInstance> activeInstances = new List<PooledSoundInstance>();
                private float fadeTargetVolume;
                private float fadeDuration;
                private float fadeElapsedTime;
                private bool isFading;

                public float Volume
                {
                    get { return volume; }
                    set
                    {
                        volume = value;
                        foreach (PooledSoundInstance instance in activeInstances)
                            instance.Instance.Volume = volume;
                    }
                }

                public void LoadSoundEffect(string name, SoundEffect soundEffect)
                {
                    soundEffects[name] = soundEffect;
                    instancePool[name] = new Stack<PooledSoundInstance>();
                }
                public void Play(string name, bool loop = false)
                {
                    if (soundEffects.TryGetValue(name, out SoundEffect soundEffect))
                    {
                        PooledSoundInstance pooledSound;

                        if (instancePool[name].Count > 0)
                            pooledSound = instancePool[name].Pop();
                        else
                        {
                            SoundEffectInstance instance = soundEffect.CreateInstance();
                            pooledSound = new PooledSoundInstance(name, instance);
                        }

                        pooledSound.Instance.Volume = volume;
                        pooledSound.Instance.IsLooped = loop;
                        pooledSound.Instance.Play();

                        activeInstances.Add(pooledSound);
                    }
                }

                public void StopAll()
                {
                    foreach (PooledSoundInstance instance in activeInstances)
                    {
                        instance.Instance.Stop();
                        instancePool[instance.ClipName].Push(instance);
                    }
                    activeInstances.Clear();
                }

                public void Update(float deltaTime)
                {
                    if (!isFading) return;

                    fadeElapsedTime += deltaTime;
                    float progress = Math.Min(fadeElapsedTime / fadeDuration, 1.0f);
                    volume = MathHelper.Lerp(volume, fadeTargetVolume, progress);

                    foreach (PooledSoundInstance instance in activeInstances)
                        instance.Instance.Volume = volume;

                    if (progress >= 1.0f)
                        isFading = false;

                    for (int i = activeInstances.Count - 1; i >= 0; i--)
                    {
                        PooledSoundInstance pooledInstance = activeInstances[i];
                        if (pooledInstance.Instance.State == SoundState.Stopped)
                        {
                            activeInstances.RemoveAt(i);
                            pooledInstance.Instance.Stop();
                            pooledInstance.Instance.Volume = volume;

                            instancePool[pooledInstance.ClipName].Push(pooledInstance);
                        }
                    }
                }
                public void FadeOutAll(float duration)
                {
                    fadeTargetVolume = 0f;
                    fadeDuration = duration;
                    fadeElapsedTime = 0;
                    isFading = true;
                }
                public void FadeInAll(float duration)
                {
                    fadeTargetVolume = 1.0f;
                    fadeDuration = duration;
                    fadeElapsedTime = 0;
                    isFading = true;
                }
                public void DisposeAllInstances(bool clearSFX)
                {
                    foreach (PooledSoundInstance instance in activeInstances)
                    {
                        instance.Instance.Stop();
                        instance.Instance.Dispose();
                    }
                    activeInstances.Clear();

                    foreach (Stack<PooledSoundInstance> pool in instancePool.Values)
                    {
                        while (pool.Count > 0)
                        {
                            PooledSoundInstance instance = pool.Pop();
                            instance.Instance.Dispose();
                        }
                        instancePool.Clear();

                        if (clearSFX)
                            soundEffects.Clear();
                    }
                }
            }
            public class VoiceManager
            {// Almost identical to SFXManager but it only allows 1 clip to play at a time, and separates the functionality
                private float volume = 1.0f;
                private Dictionary<string, SoundEffect> voiceClips = new Dictionary<string, SoundEffect>();
                private Dictionary<string, Stack<PooledSoundInstance>> instancePool = new Dictionary<string, Stack<PooledSoundInstance>>();
                private PooledSoundInstance? currentVoiceClip;
                private float fadeTargetVolume;
                private float fadeDuration;
                private float fadeElapsedTime;
                private bool isFading;
                public float Volume
                {
                    get { return volume; }
                    set
                    {
                        volume = value;
                        if (currentVoiceClip != null)
                            currentVoiceClip.Instance.Volume = value;
                    }
                }

                public void LoadVoiceClip(string name, SoundEffect voiceClip)
                {
                    voiceClips[name] = voiceClip;
                    instancePool[name] = new Stack<PooledSoundInstance>();
                }
                public void Play(string name)
                {
                    if (currentVoiceClip != null && currentVoiceClip.Instance.State == SoundState.Playing)
                    {
                        currentVoiceClip.Instance.Stop();
                        instancePool[currentVoiceClip.ClipName].Push(currentVoiceClip);
                    }

                    if (voiceClips.TryGetValue(name, out SoundEffect voiceClip))
                    {
                        PooledSoundInstance pooledVoice;

                        if (instancePool[name].Count > 0)
                            pooledVoice = instancePool[name].Pop();
                        else
                        {
                            SoundEffectInstance instance = voiceClip.CreateInstance();
                            pooledVoice = new PooledSoundInstance(name, instance);
                        }

                        pooledVoice.Instance.Volume = volume;
                        pooledVoice.Instance.Play();

                        currentVoiceClip = pooledVoice;
                    }
                }
                public void Stop()
                {
                    if (currentVoiceClip != null)
                    {
                        currentVoiceClip.Instance.Stop();
                        instancePool[currentVoiceClip.ClipName].Push(currentVoiceClip);
                        currentVoiceClip = null;
                    }
                }
                public void FadeOut(float duration)
                {
                    if (currentVoiceClip == null) return;

                    fadeTargetVolume = 0f;
                    fadeDuration = duration;
                    fadeElapsedTime = 0f;
                    isFading = true;
                }
                public void FadeIn(string name, float targetVolume, float duration)
                {
                    Play(name);
                    volume = 0f;
                    Volume = volume;
                    fadeTargetVolume = targetVolume;
                    fadeDuration = duration;
                    fadeElapsedTime = 0;
                    isFading = true;
                }
                public void Update(float deltaTime)
                {
                    if (isFading && currentVoiceClip != null)
                    {
                        fadeElapsedTime += deltaTime;
                        float progress = Math.Min(fadeElapsedTime / fadeDuration, 1.0f);
                        volume = MathHelper.Lerp(volume, fadeTargetVolume, progress);
                        currentVoiceClip.Instance.Volume = volume;

                        if (progress >= 1.0f)
                        {
                            if (fadeTargetVolume == 0)
                                Stop();

                            isFading = false;
                        }
                    }
                }
                public void DisposeAllInstances(bool clearClips)
                {
                    if (currentVoiceClip != null)
                    {
                        currentVoiceClip.Instance.Stop();
                        currentVoiceClip.Instance.Dispose();
                        currentVoiceClip = null;
                    }

                    foreach (Stack<PooledSoundInstance> pool in instancePool.Values)
                    {
                        while (pool.Count > 0)
                        {
                            PooledSoundInstance instance = pool.Pop();
                            instance.Instance.Dispose();
                        }
                        instancePool.Clear();

                        if (clearClips)
                            voiceClips.Clear();
                    }
                }
            }

            public class AudioManager
            {
                public MusicManager Music { get; private set; }
                public SFXManager Sfx { get; private set; }
                public VoiceManager Voice { get; private set; }
                public AudioManager()
                {
                    Music = new MusicManager();
                    Sfx = new SFXManager();
                    Voice = new VoiceManager();
                }
                public AudioManager(bool music, bool sfx, bool voice) : this()
                {
                    Music = music ? new MusicManager() : null;
                    Sfx = sfx ? new SFXManager() : null;
                    Voice = voice ? new VoiceManager() : null;
                }
                public void Update(float deltaTime)
                {
                    Music.Update(deltaTime);
                    Sfx.Update(deltaTime);
                    Voice.Update(deltaTime);
                }
                public void Dispose(bool clearSounds, bool clearVoices)
                {
                    Music.Dispose();
                    Sfx.DisposeAllInstances(clearSounds);
                    Voice.DisposeAllInstances(clearVoices);
                }

            }
        }

        namespace Data
        {
            public class FileManager
            {
                private readonly string saveDirectory;

                public FileManager(string saveDirectoryRoot)
                {
                    saveDirectory = saveDirectoryRoot;

                    if (!Directory.Exists(saveDirectory))
                        Directory.CreateDirectory(saveDirectory);
                }
                public void SaveData<T>(string fileName, T data)
                {
                    string filePath = Path.Combine(saveDirectory, fileName);

                    SerializationManager.SaveToFile(filePath, data);
                }
                public T LoadData<T>(string fileName)
                {
                    string filePath = Path.Combine(saveDirectory, fileName);

                    return SerializationManager.LoadFromFile<T>(filePath);
                }
            }
            public static class SerializationManager
            {
                public static void SaveToFile<T>(string filePath, T data)
                {
                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

                    File.WriteAllText(filePath, json);
                }
                public static T LoadFromFile<T>(string filePath)
                {
                    if (File.Exists(filePath) == false)
                        return default;

                    var json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<T>(json);
                }
            }
        }
        namespace Debugging
        {
            public enum LogLevel
            { Info, Warning, Error }

            public class DebugLogger
            {
                private static DebugLogger instance;
                private string logFilePath;
                private LogLevel logLevel;

                public static DebugLogger Instance => instance ?? (instance = new DebugLogger());
                private DebugLogger()
                {
                    LoadSettings();
                    if (!string.IsNullOrEmpty(logFilePath))
                        Directory.CreateDirectory(logFilePath);
                }
                public void LoadSettings()
                {
                    logFilePath = ConfigManager.Instance.GetSetting("Debug.LogFilePath", "logs/debug.log");
                    logLevel = Enum.TryParse(ConfigManager.Instance.GetSetting("Debug.LogLevel", "Info"), out LogLevel level) ? level : LogLevel.Info;
                }
                public void Log(string message, LogLevel level = LogLevel.Info)
                {
                    if (level >= logLevel)
                    {
                        string logMessage = $"{DateTime.Now:G} [{level}] {message}";
                        Console.WriteLine(logMessage);

                        if (!string.IsNullOrEmpty(logFilePath))
                            File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
                    }
                }
            }
        }
    }
}
    