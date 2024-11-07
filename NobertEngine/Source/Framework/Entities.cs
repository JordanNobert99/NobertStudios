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
    namespace Entities
    {
        namespace Base
        {
            public class Entity
            {
                private static int nextId = 0;
                public int Id { get; private set; }
                private Dictionary<Type, IComponent> components = new Dictionary<Type, IComponent>();
                public Entity()
                {
                    Id = nextId++;
                }
                public override string ToString()
                {
                    return $"Entity_{Id}";
                }
            }
            public class EntityManager
            {
                private Dictionary<int, Entity> entities = new Dictionary<int, Entity>();
                private Dictionary<int, Dictionary<Type, IComponent>> entityComponents = new Dictionary<int, Dictionary<Type, IComponent>>();
                private List<ComponentSystem> systems = new List<ComponentSystem>();

                public Entity CreateEntity()
                {
                    Entity entity = new Entity();
                    entities[entity.Id] = entity;
                    entityComponents[entity.Id] = new Dictionary<Type, IComponent>();
                    return entity;
                }
                public void DestroyEntity(Entity entity)
                {
                    if (entities.Remove(entity.Id))
                    {
                        entityComponents.Remove(entity.Id);

                        foreach (ComponentSystem system in systems)
                            system.RemoveEntity(entity);

                        // Remember to Dispose of object
                    }
                }
                public void AddComponent<T>(Entity entity, T component) where T : IComponent
                {
                    if (entityComponents.ContainsKey(entity.Id))
                    {
                        entityComponents[entity.Id][typeof(T)] = component;

                        foreach (ComponentSystem system in systems)
                            system.AddEntity(entity);
                    }
                }
                public T GetComponent<T>(Entity entity) where T : IComponent
                {
                    if (entityComponents.ContainsKey(entity.Id) && entityComponents[entity.Id].TryGetValue(typeof(T), out var component))
                        return (T)component;
                    return default;
                }
                public List<IComponent> GetAllComponents(Entity entity)
                {
                    return entityComponents[entity.Id].Values.ToList();
                }
                public void RemoveComponent<T>(Entity entity) where T : IComponent
                {
                    if (entityComponents.ContainsKey(entity.Id))
                    {
                        entityComponents[entity.Id].Remove(typeof(T));

                        foreach (ComponentSystem system in systems)
                            system.RemoveEntity(entity);
                    }
                }
                public bool HasComponent<T>(Entity entity) where T : IComponent
                {
                    return entityComponents.ContainsKey(entity.Id) && entityComponents[entity.Id].ContainsKey(typeof(T));
                }
                public void AddSystem(ComponentSystem system)
                {
                    systems.Add(system);

                    foreach (Entity entity in entities.Values)
                        system.AddEntity(entity);
                }
                public void RemoveSystem(ComponentSystem system)
                {
                    foreach (Entity entity in entities.Values)
                        system.RemoveEntity(entity);

                    systems.Remove(system);

                    // Dispose of system
                }
                public void UpdateSystems(float deltaTime)
                {
                    foreach (ComponentSystem system in systems)
                        system.Update(deltaTime);
                }
                public void DrawEntities(SpriteBatch spriteBatch, Vector2 viewPortCenter, int drawDistance)
                {
                    foreach (DrawSystem system in systems)
                        system.Draw(spriteBatch, viewPortCenter, drawDistance);
                }
                public List<Entity> GetEntitiesWithComponents<T>() where T : IComponent
                {
                    List<Entity> entityList = new List<Entity>();
                    foreach (int entityId in entities.Keys)
                    {
                        if (entityComponents[entityId].ContainsKey(typeof(T)))
                            entityList.Add(entities[entityId]);
                    }
                    return entityList;
                }
                public List<Entity> GetAllEntities()
                {
                    return entities.Values.ToList();
                }
            }
        }

        namespace Components
        {
            public interface IComponent { };

            namespace Physics
            {
                public class SpriteComponent : IComponent
                {
                    public Texture2D Texture {  get; set; }
                    public Vector2 Origin { get; set; } = Vector2.Zero;
                    public float Scale { get; set; } = 1f;
                    public Color Color { get; set; }  = Color.White;
                    public float Layer { get; set; }  = 0f;

                    public SpriteComponent(Texture2D texture)
                    {
                        Texture = texture;
                    }
                }
                public class AnimatedSpriteComponent : SpriteComponent
                {
                    public Animator Animator { get; private set; }

                    public AnimatedSpriteComponent(Animator animator, Texture2D texture) : base(texture)
                    {
                        Animator = animator;
                    }
                }
                public class PositionComponent : IComponent
                {
                    public float X { get; set; }
                    public float Y { get; set; }

                    public PositionComponent(float x = 0, float y = 0)
                    {
                        X = x;
                        Y = y;
                    }
                    public void SetPosition(float x, float y)
                    {
                        X = x;
                        Y = y;
                    }
                    public void SetPosition(Vector2 position)
                    {
                        X = position.X;
                        Y = position.Y;
                    }
                }
                public class RotationComponent : IComponent
                {
                    public float Angle { get; private set; }
                    public Vector2 TargetDirection { get; set; }
                    public float Speed { get; set; }

                    public RotationComponent(float initialAngle, float speed)
                    {
                        Angle = initialAngle;
                        Speed = speed;
                    }

                    public void Rotate(float amount)
                    {
                        Angle = (Angle + amount) % 360;//Keeps within 0-359;
                    }
                    public void SetAngle(float angle)
                    {
                        Angle = (angle + 360) % 360;
                    }
                }
                public class VelocityComponent : IComponent
                {
                    public static readonly Type[] RequiredComponents = { typeof(PositionComponent) };
                    public float X { get; set; }
                    public float Y { get; set; }
                    public float GetSpeed() => (float)Math.Sqrt(X * X + Y * Y);
                    public VelocityComponent(float x, float y)
                    {
                        X = x;
                        Y = y;
                    }
                    public void SetVelocity(float x, float y)
                    {
                        X = x;
                        Y = y;
                    }
                    public void SetVelocity(Vector2 velocity)
                    {
                        X = velocity.X;
                        Y = velocity.Y;
                    }
                    public void SetVelocityFromSpeedDegrees(float speed, float angleInDegrees)
                    {
                        float angleInRadians = MathHelper.ToRadians(angleInDegrees);
                        X = speed * (float)Math.Cos(angleInRadians);
                        Y = speed * (float)Math.Sin(angleInRadians);
                    }
                    public void SetVelocityFromSpeedRadians(float speed, float angleInRadians)
                    {
                        X = speed * (float)Math.Cos(angleInRadians);
                        Y = speed * (float)Math.Sin(angleInRadians);
                    }
                    public void AddForce(float xForce, float yForce)
                    {
                        X += xForce;
                        Y += yForce;
                    }
                    public void AddForce(Vector2 force)
                    {
                        X += force.X;
                        Y += force.Y;
                    }
                    public void ApplyDrag(float dragFactor, float deltaTime)
                    {
                        X *= 1 - dragFactor * deltaTime;
                        Y *= 1 - dragFactor * deltaTime;
                    }
                    public void ClampSpeed(float maxSpeed)
                    {
                        float currentSpeed = GetSpeed();
                        if (currentSpeed > maxSpeed)
                        {
                            float scaleFactor = maxSpeed / currentSpeed;
                            X *= scaleFactor;
                            Y *= scaleFactor;
                        }
                    }
                    public void Bounce(float normalX, float normalY)
                    {
                        float magnitude = (float)Math.Sqrt(normalX * normalX + normalY * normalY);
                        normalX /= magnitude;
                        normalY /= magnitude;

                        float dotProduct = X * normalX + Y * normalY;
                        X -= 2 * dotProduct * normalX;
                        Y -= 2 * dotProduct * normalY;
                    }
                }
                public class ColliderComponent : IComponent
                {
                    public float X { get; set; }
                    public float Y { get; set; }
                    public float Width { get; private set; }
                    public float Height { get; private set; }
                    public event Action<ColliderComponent> OnCollision;
                    public ColliderComponent(float x, float y, float width, float height)
                    {
                        X = x;
                        Y = y;
                        Width = width;
                        Height = height;
                    }
                    public bool Intersects(ColliderComponent other)
                    {
                        return
                            X < other.X + other.Width &&
                            X + Width > other.X &&
                            Y < other.Y + other.Height &&
                            Y + Height > other.Y;
                    }
                    public void TriggerCollision(ColliderComponent other)
                    {
                        OnCollision?.Invoke(other);
                    }
                }
                public class AIComponent : IComponent
                {
                    public enum AIState
                    { Idle, Attack, Patrol, Chase, Guard, Return }

                    public AIState CurrentState { get; set; }
                    public Entity Target { get; set; }
                    public float DetectionRange { get; set; }
                    public float GuardRange { get; set; }
                    public float AttackRange { get; set; }
                    public AIComponent(float detectionRange, float guardRange, float attackRange)
                    {
                        CurrentState = AIState.Idle;
                        DetectionRange = detectionRange;
                        GuardRange = guardRange;
                        AttackRange = attackRange;
                        Target = null;
                    }

                }
            }
            namespace Stats
            {
                public class HealthComponent : IComponent
                {
                    public int MaxHealth { get; private set; }
                    public int CurrentHealth { get; private set; }

                    public HealthComponent(int maxHealth)
                    {
                        MaxHealth = maxHealth;
                        CurrentHealth = MaxHealth;
                    }

                    public void TakeDamage(int amount)
                    {
                        CurrentHealth = Math.Max(CurrentHealth - amount, 0);
                    }
                    public void Heal(int amount)
                    {
                        CurrentHealth = Math.Min(CurrentHealth + amount, MaxHealth);
                    }
                    public bool IsAlive() => CurrentHealth > 0;
                }
            }
        }
        namespace Systems
        {
            public abstract class ComponentSystem
            {
                protected EntityManager entityManager;
                public Dictionary<Entity, List<IComponent>> entities { get; private set; }

                protected ComponentSystem(EntityManager _entityManager) 
                { 
                    entityManager = _entityManager;
                }

                public abstract void Update(float deltaTime);
                public virtual void AddEntity(Entity entity)
                {
                    entities[entity] = entityManager.GetAllComponents(entity);
                }
                public virtual void RemoveEntity(Entity entity)
                {
                    entities.Remove(entity);
                }
                public virtual List<Entity> GetEntityList()
                {
                    return entities.Keys.ToList();
                }
                public virtual List<IComponent> GetComponentList()
                {
                    List<IComponent> returnList = new List<IComponent>();
                    foreach (Entity entity in entities.Keys)
                        returnList.AddRange(entityManager.GetAllComponents(entity));
                    return returnList;
                }
                public virtual void UpdateComponentDictionary()
                {
                    foreach (Entity entity in entities.Keys)
                    {
                        foreach (IComponent component in entityManager.GetAllComponents(entity))
                        {
                            if (entities[entity].Contains(component) == false)
                                entities[entity].Add(component);
                        }
                    }
                }
            }
            namespace Draw
            {
                public class DrawSystem : ComponentSystem
                {
                    private SpriteBatch spriteBatch;
                    private List<SpriteComponent> spriteComponents = new List<SpriteComponent>();
                    private List<AnimatedSpriteComponent> animatedComponents = new List<AnimatedSpriteComponent>();
                    private List<PositionComponent> positionComponents = new List<PositionComponent>();
                    private List<RotationComponent> rotationComponents = new List<RotationComponent>();
                    private List<PositionComponent> animatedPositionComponents = new List<PositionComponent>();
                    private List<RotationComponent> animatedRotationComponents = new List<RotationComponent>();

                    public DrawSystem(EntityManager entityManager) : base(entityManager)
                    {
                        foreach (Entity entity in entityManager.GetEntitiesWithComponents<SpriteComponent>())
                            spriteComponents.Add(entityManager.GetComponent<SpriteComponent>(entity));
                        foreach (Entity entity in entityManager.GetEntitiesWithComponents<AnimatedSpriteComponent>())
                            animatedComponents.Add(entityManager.GetComponent<AnimatedSpriteComponent>(entity));
                        foreach (Entity entity in entityManager.GetEntitiesWithComponents<PositionComponent>())
                        {
                            if (spriteComponents.Contains(entityManager.GetComponent<SpriteComponent>(entity)))
                                positionComponents.Add(entityManager.GetComponent<PositionComponent>(entity));
                            else if (animatedComponents.Contains(entityManager.GetComponent<AnimatedSpriteComponent>(entity)))
                                animatedPositionComponents.Add(entityManager.GetComponent<PositionComponent>(entity));
                        }
                        foreach (Entity entity in entityManager.GetEntitiesWithComponents<RotationComponent>())
                        {
                            if (spriteComponents.Contains(entityManager.GetComponent<SpriteComponent>(entity)))
                                rotationComponents.Add(entityManager.GetComponent<RotationComponent>(entity));
                            else if (animatedComponents.Contains(entityManager.GetComponent<AnimatedSpriteComponent>(entity)))
                                animatedRotationComponents.Add(entityManager.GetComponent<RotationComponent>(entity));
                        }
                    }
                    public override void Update(float deltaTime)
                    {
                        foreach (AnimatedSpriteComponent animatedSprite in animatedComponents)
                            animatedSprite.Animator.Update(deltaTime);
                    }
                    public void Draw(SpriteBatch spriteBatch, Vector2 viewPortCenter, int drawDistance)
                    {
                        for (int i = 0; i < spriteComponents.Count; i++)
                        {
                            SpriteComponent sprite = spriteComponents[i];
                            Vector2 position = new Vector2(positionComponents[i].X, positionComponents[i].Y);
                            float rotation = rotationComponents[i].Angle; 
                            Rectangle rectangle = new Rectangle( (int)position.X, (int)position.Y, sprite.Texture.Width, sprite.Texture.Height);

                            if (Vector2.Distance(position, viewPortCenter) <= drawDistance)
                                spriteBatch.Draw(sprite.Texture, position, rectangle, sprite.Color, rotation, sprite.Origin, sprite.Scale, SpriteEffects.None, sprite.Layer);
                        }

                        for (int i = 0; i < animatedComponents.Count; i++)
                        {
                            AnimatedSpriteComponent sprite = animatedComponents[i];
                            Vector2 position = new Vector2(animatedPositionComponents[i].X, animatedPositionComponents[i].Y);
                            float rotation = animatedRotationComponents[i].Angle;

                            if (Vector2.Distance(position, viewPortCenter) <= drawDistance)
                                animatedComponents[i].Animator.Draw(spriteBatch, position, rotation, sprite.Origin, sprite.Scale);
                        }
                    }
                }
            }

            namespace Physics
            {
                public class MovementSystem : ComponentSystem
                {
                    private List<PositionComponent> positionComponents = new List<PositionComponent>();
                    private List<VelocityComponent> velocityComponents = new List<VelocityComponent>();
                    public MovementSystem(EntityManager entityManager) : base(entityManager)
                    {
                        foreach (Entity entity in entityManager.GetEntitiesWithComponents<VelocityComponent>())
                        {
                            if (entityManager.GetComponent<PositionComponent>(entity) != null)
                            {
                                PositionComponent entityPosition = entityManager.GetComponent<PositionComponent>(entity);
                                positionComponents.Add(entityPosition);

                                VelocityComponent entityVelocity = entityManager.GetComponent<VelocityComponent>(entity);
                                velocityComponents.Add(entityVelocity);
                            }
                        }
                    }
                    public override void Update(float deltaTime)
                    {
                        for (int i = 0; i < entities.Count; i++)
                        {
                            PositionComponent positionComponent = positionComponents[i];
                            VelocityComponent velocityComponent = velocityComponents[i];

                            if (positionComponent == null || velocityComponent == null)
                                return;

                            positionComponent.X += velocityComponent.X * deltaTime;
                            positionComponent.Y += velocityComponent.Y * deltaTime;
                        }
                    }
                }
                public class RotationSystem : ComponentSystem
                {
                    private List<RotationComponent> rotationComponents = new List<RotationComponent>();
                    public RotationSystem(EntityManager entityManager) : base(entityManager)
                    {
                        foreach (Entity entity in entityManager.GetEntitiesWithComponents<RotationComponent>())
                            rotationComponents.Add(entityManager.GetComponent<RotationComponent>(entity));
                    }
                    public override void Update(float deltaTime)
                    {
                        foreach (RotationComponent component in rotationComponents)
                        {
                            if (component.Speed == 0)
                                return;

                            float targetAngle = MathHelper.ToDegrees((float)Math.Atan2(component.TargetDirection.Y, component.TargetDirection.X));
                            float angleDifference = (targetAngle - component.Angle + 360) % 360;

                            if (angleDifference > 180)//Clamp between -180 to 180
                                angleDifference -= 360;

                            float angleChange = Math.Clamp(angleDifference, -component.Speed * deltaTime, component.Speed * deltaTime);
                            component.Rotate(angleChange);
                        }
                    }
                }
                public class GravitySystem : ComponentSystem
                {
                    private float gravityX;
                    private float gravityY;
                    private List<VelocityComponent> velocityComponents = new List<VelocityComponent>();
                    public GravitySystem(EntityManager entityManager, float _gravityX, float _gravityY) : base(entityManager)
                    {
                        gravityX = _gravityX;
                        gravityY = _gravityY;

                        foreach (Entity entity in entityManager.GetEntitiesWithComponents<VelocityComponent>())
                            velocityComponents.Add(entityManager.GetComponent<VelocityComponent>(entity));
                    }
                    public override void Update(float deltaTime)
                    {
                        for (int i = 0; i < velocityComponents.Count; i++)
                            velocityComponents[i].AddForce(gravityX * deltaTime, gravityY * deltaTime);
                    }
                }
                public class DragSystem : ComponentSystem
                {
                    private readonly float dragFactor;
                    private List<VelocityComponent> velocityComponents = new List<VelocityComponent>();
                    public DragSystem(EntityManager entityManager, float _dragFactor) : base(entityManager)
                    {
                        dragFactor = _dragFactor;

                        foreach (Entity entity in entityManager.GetEntitiesWithComponents<VelocityComponent>())
                            velocityComponents.Add(entityManager.GetComponent<VelocityComponent>(entity));
                    }
                    public override void Update(float deltaTime)
                    {
                        foreach (VelocityComponent component in velocityComponents)
                            component.ApplyDrag(dragFactor, deltaTime);
                    }
                }
                public class CollisionSystem : ComponentSystem
                {
                    private List<ColliderComponent> colliderComponents = new List<ColliderComponent>();
                    public CollisionSystem(EntityManager entityManager) : base(entityManager)
                    {
                        foreach (Entity entity in entityManager.GetEntitiesWithComponents<ColliderComponent>())
                            colliderComponents.Add(entityManager.GetComponent<ColliderComponent>(entity));
                    }
                    public override void Update(float deltaTime)
                    {
                        for (int i = 0; i < colliderComponents.Count; i++)
                        {
                            if (colliderComponents[i].GetType() != typeof(ColliderComponent))
                                return;

                            for (int j = i + 1; j < colliderComponents.Count; j++)
                            {
                                if (colliderComponents[j].GetType() != typeof(ColliderComponent))
                                    return;

                                ColliderComponent colliderA = colliderComponents[i] as ColliderComponent;
                                ColliderComponent colliderB = colliderComponents[j] as ColliderComponent;
                                if (colliderA.Intersects(colliderB))
                                {
                                    colliderA.TriggerCollision(colliderB);
                                    colliderB.TriggerCollision(colliderA);
                                }
                            }
                        }
                    }
                }
            }
            namespace Stats
            {
                public class HealthSystem : ComponentSystem
                {
                    private List<HealthComponent> healthComponents = new List<HealthComponent>();
                    public HealthSystem(EntityManager entityManager) : base(entityManager)
                    {
                        foreach (Entity entity in entityManager.GetEntitiesWithComponents<HealthComponent>())
                            healthComponents.Add(entityManager.GetComponent<HealthComponent>(entity));
                    }
                    public override void Update(float deltaTime)
                    {
                        for (int i = 0; i < healthComponents.Count; i++)
                        {
                            if (healthComponents[i].IsAlive() == false)//Add death functionality
                                return;
                        }
                    }
                }
            }
            namespace AI
            {
                public class AISystem : ComponentSystem
                {

                    private List<AIComponent> aiComponents = new List<AIComponent>();
                    private List<PositionComponent> positionComponents = new List<PositionComponent>();

                    public AISystem(EntityManager entityManager) : base(entityManager)
                    {
                        foreach (Entity entity in entityManager.GetEntitiesWithComponents<AIComponent>())
                        {
                            if (entityManager.GetComponent<PositionComponent>(entity) != null)
                            {
                                PositionComponent entityPosition = entityManager.GetComponent<PositionComponent>(entity);
                                positionComponents.Add(entityPosition);

                                AIComponent entityAi = entityManager.GetComponent<AIComponent>(entity);
                                aiComponents.Add(entityAi);
                            }
                        }
                    }

                    public override void Update(float deltaTime)
                    {
                        for (int i = 0; i < entities.Count; i++)
                        {
                            AIComponent aiComponent = aiComponents[i];
                            PositionComponent positionComponent = positionComponents[i];

                            if (aiComponent == null || positionComponent == null)
                                return;

                            UpdateAIState(aiComponent, positionComponent, deltaTime);
                        }
                    }
                    private void UpdateAIState(AIComponent ai, PositionComponent position, float deltaTime)
                    {
                        switch (ai.CurrentState)
                        {
                            case AIComponent.AIState.Idle:
                                HandleIdleState(ai, position);
                                break;
                            case AIComponent.AIState.Attack:
                                HandleAttackState(ai, position, deltaTime);
                                break;
                            case AIComponent.AIState.Patrol:
                                HandlePatrolState(ai, position, deltaTime);
                                break;
                            case AIComponent.AIState.Chase:
                                HandleChaseState(ai, position, deltaTime);
                                break;
                            case AIComponent.AIState.Guard:
                                HandleGuardState(ai, position, deltaTime);
                                break;
                            case AIComponent.AIState.Return:
                                HandleReturnState(ai, position, deltaTime);
                                break;
                            default:
                                break;
                        }
                    }
                    private void HandleIdleState(AIComponent ai, PositionComponent position)
                    {
                        Entity target = FindTargetInRange(ai, position, ai.DetectionRange);
                        if (target != null)
                        {
                            ai.Target = target;
                            ai.CurrentState = AIComponent.AIState.Chase;
                        }
                    }
                    private void HandleAttackState(AIComponent ai, PositionComponent position)
                    {
                        // Handle Stand still attacking
                    }
                    private void HandleAttackState(AIComponent ai, PositionComponent position, float deltaTime)
                    {

                    }
                    private void HandlePatrolState(AIComponent ai, PositionComponent position, float deltaTime)
                    {
                        // Move along a path or in an alternating random direction
                    }
                    private void HandleChaseState(AIComponent ai, PositionComponent position, float deltaTime)
                    {
                        if (ai.Target != null)
                        {
                            PositionComponent targetPosition = entityManager.GetComponent<PositionComponent>(ai.Target);
                            if (targetPosition != null)
                            {
                                float distance = Vector2.Distance(new Vector2(position.X, position.Y), new Vector2(targetPosition.X, targetPosition.Y));

                                if (distance <= ai.AttackRange)
                                    ai.CurrentState = AIComponent.AIState.Attack;
                                else
                                {
                                    Vector2 direction = Vector2.Normalize(new Vector2(targetPosition.X - position.X, targetPosition.Y - position.Y));

                                    position.X += direction.X * deltaTime;
                                    position.Y += direction.Y * deltaTime;
                                }
                            }
                        }
                        else
                            ai.CurrentState = AIComponent.AIState.Idle; // Or last state should be implemented
                    }
                    private void HandleGuardState(AIComponent ai, PositionComponent position, float deltaTime)
                    {
                        // Handle guarding logic
                    }
                    private void HandleReturnState(AIComponent ai, PositionComponent position, float deltaTime)
                    {
                        // Handle returning to target location
                    }
                    private Entity FindTargetInRange(AIComponent ai, PositionComponent position, float range)
                    {
                        foreach (Entity potentialTarget in entityManager.GetEntitiesWithComponents<PositionComponent>())
                        {
                            if (potentialTarget != ai.Target)
                            {
                                PositionComponent targetPosition = entityManager.GetComponent<PositionComponent>(potentialTarget);
                                float distance = Vector2.Distance(new Vector2(position.X, position.Y), new Vector2(targetPosition.X, targetPosition.Y));
                                if (distance <= range)
                                    return potentialTarget;
                            }
                        }
                        return null;
                    }
                }
            }
        }
    }
}
