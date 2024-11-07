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
    namespace Scenes
    {
        namespace Creation
        {
            public class Scene
            {
                private List<Texture2D> backgrounds = new List<Texture2D>();
                private List<Element> uiElements = new List<Element>();
                private List<EntityManager> entityManagers = new List<EntityManager>();
                public virtual void Load() { }
                public virtual void Unload() { }// Perform disposal cleanup here
                public void AddBackground(Texture2D texture)
                {
                    backgrounds.Add(texture);
                }
                public void AddUIElement(Element element)
                {
                    uiElements.Add(element);
                }
                public void AddEntityManager(EntityManager entityManager)
                {
                    entityManagers.Add(entityManager);
                }
                public void Update(GameTime gameTime)
                {
                    float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

                    foreach (Element element in uiElements)
                        element.Update(gameTime);

                    foreach (EntityManager entityManager in entityManagers)
                        entityManager.UpdateSystems(delta);
                }
                public void Draw(SpriteBatch spriteBatch, Texture2D uiBaseTexture, SpriteFont font, Vector2 viewPortCenter, int drawDistance)
                {
                    foreach (Texture2D background in backgrounds)
                        spriteBatch.Draw(background, Vector2.Zero, Color.White);

                    foreach (Element element in uiElements)
                    {
                        if (element is Slider slider)
                            element.Draw(spriteBatch, font);
                        else
                            element.Draw(spriteBatch, uiBaseTexture);
                    }

                    foreach (EntityManager entityManager in entityManagers)
                        entityManager.DrawEntities(spriteBatch, viewPortCenter, drawDistance);
                }
            }
            public class SceneBuilder
            {
                private List<Texture2D> backgrounds = new List<Texture2D>();
                private List<Element> uiElements = new List<Element>();
                private List<EntityManager> entityManagers = new List<EntityManager>();

                public SceneBuilder AddBackground(Texture2D texture)
                {
                    backgrounds.Add(texture);
                    return this;
                }
                public SceneBuilder AddUIElement(Element element)
                {
                    uiElements.Add(element);
                    return this;
                }
                public SceneBuilder AddEntityManager(EntityManager entityManager)
                {
                    entityManagers.Add(entityManager);
                    return this;
                }
                public Scene Build()
                {
                    var scene = new Scene();

                    foreach (Texture2D background in backgrounds)   
                        scene.AddBackground(background);
                    foreach (Element element in uiElements)
                        scene.AddUIElement(element);
                    foreach (EntityManager entityManager in entityManagers)
                        scene.AddEntityManager(entityManager);

                    return scene;
                }
            }
        }
        namespace Management
        {
            public class SceneManager
            {
                private Stack<Scene> sceneStack = new Stack<Scene>();

                public Scene ActiveScene => sceneStack.Count > 0 ? sceneStack.Peek() : null;

                public void LoadScene(Scene scene)
                {
                    ActiveScene?.Unload();
                    sceneStack.Push(scene);
                    scene.Load();
                }
                public void PreviousScene()
                {
                    if (sceneStack.Count > 0)
                    {
                        Scene scene = sceneStack.Pop();
                        scene.Unload();

                        ActiveScene?.Load();
                    }
                }
                public void SetSceneStack(Scene firstScene)// Major Transitions eg: Back to Main Menu (not in normal que)
                {
                    while(sceneStack.Count > 0)
                    {
                        Scene scene = sceneStack.Pop();
                        scene.Unload();
                    }

                    sceneStack.Push(firstScene);
                    firstScene.Load();
                }
                public void BringSceneToFront(Scene targetScene)
                {
                    if (sceneStack.Count == 0 || ActiveScene == targetScene)
                        return;

                    List<Scene> sceneAboveStorage = new List<Scene>();
                    while(sceneStack.Count > 0 && ActiveScene != targetScene)
                        sceneAboveStorage.Add(sceneStack.Pop());

                    if (sceneStack.Count == 0)// If scene not found restore scene que and exit
                    {
                        for (int i = 0; i < sceneAboveStorage.Count; i++)
                            sceneStack.Push(sceneAboveStorage[i]);
                        return;
                    }

                    Scene target = sceneStack.Pop();
                    sceneStack.Push(target);
                    for (int i = 0; i < sceneAboveStorage.Count; i++)
                        sceneStack.Push(sceneAboveStorage[i]);
                }
                public void Update(GameTime gameTime)
                {
                    ActiveScene?.Update(gameTime);
                }
                public void Draw(SpriteBatch spriteBatch, SpriteFont font, Vector2 viewPortCenter, int drawDistance)
                {
                    ActiveScene?.Draw(spriteBatch, UIResource.WhiteTexture, font, viewPortCenter, drawDistance);
                }
            }
        }
        namespace Cutscenes
        {
            public interface ICutsceneElement
            {
                bool IsComplete { get; }
                void Update(GameTime gameTime, bool skip);
                public void Draw(SpriteBatch spriteBatch, Vector2 position, Color color, SpriteFont font = null);
            }
            public class DialogElement : ICutsceneElement
            {
                private string text;
                private bool isComplete;

                public bool IsComplete => isComplete;
                public DialogElement(string _text)
                {
                    text = _text;
                    isComplete = false;
                }
                public void Update(GameTime gameTime, bool skip)
                {
                    isComplete = skip;
                }
                public void Draw(SpriteBatch spriteBatch, Vector2 position, Color color, SpriteFont font = null)
                {
                    spriteBatch.DrawString(font, text, position, color);
                }
            }
            public class Cutscene
            {
                private List<ICutsceneElement> elements;
                private int currentIndex = 0;
                public Vector2 Position { get; set; }
                private Color Color;
                public SpriteFont Font { get; private set; }

                public Cutscene(List<ICutsceneElement> elements, Vector2 position, Color color, SpriteFont font)
                {
                    this.elements = elements;
                    Position = position;
                    Color = color;
                    Font = font;
                }
                public bool IsComplete => currentIndex >= elements.Count;

                public void Update(GameTime gameTime, bool skip)
                {
                    if (IsComplete)
                        return;

                    ICutsceneElement currentElement = elements[currentIndex];
                    currentElement.Update(gameTime, skip);

                    if (currentElement.IsComplete)
                        currentIndex++;
                }
                public void Draw(SpriteBatch spriteBatch)
                {
                    if (!IsComplete)
                        elements[currentIndex].Draw(spriteBatch, Position, Color, Font);
                }
            }
            public class CutsceneManager
            { 
                private Queue<Cutscene> cutscenes = new Queue<Cutscene>();
                public bool IsPlaying => cutscenes.Count > 0;
                public void PlayCutscene(Cutscene cutscene)
                {
                    cutscenes.Enqueue(cutscene);
                }
                public void Update(GameTime gameTime, bool skip)
                {
                    if (!IsPlaying)
                        return;

                    Cutscene activeCutscene = cutscenes.Peek();
                    activeCutscene.Update(gameTime, skip);

                    if (activeCutscene.IsComplete)
                        cutscenes.Dequeue();
                }
                public void Draw(SpriteBatch spriteBatch)
                {
                    if (!IsPlaying)
                        return;

                    Cutscene activeCutscene = cutscenes.Peek();
                    activeCutscene.Draw(spriteBatch);
                }
            }
        }
    }
}
