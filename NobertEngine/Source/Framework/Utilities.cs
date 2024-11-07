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
    namespace Utilities
    {
        namespace Time
        {
            public class Clock
            {
                public int Hours { get; private set; }
                public int Minutes { get; private set; }
                public int Seconds { get; private set; }
                public int Day { get; private set; }
                public int Month { get; private set; }
                public int Year { get; private set; }

                private float timeScale;
                private float timer;
                private readonly bool useLunarMonths;
                private readonly int lunarMonthLength = 29;
                private readonly int[] daysPerCalendarMonth = 
                    { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
                private readonly string[] monthNames = 
                    { "January", "February", "March", "April", "May", "June", 
                    "July", "August", "September", "October", "November", "December" };

                public Action<int> OnHourPassed;
                public Action<int> OnNewDay;
                public Action<int> OnNewMonth;
                public Action<int> OnNewYear;
                public Clock(int startHour = 6, int startMinute = 0, int startSecond = 0, float _timeScale = 60f, bool _useLunarMonths = false)
                {
                    Hours = startHour;
                    Minutes = startMinute;
                    Seconds = startSecond;
                    Day = 1;
                    Month = 1;
                    Year = 1;
                    timeScale = _timeScale;
                    useLunarMonths = _useLunarMonths;

                    timer = 0;
                }
                public void Update(float deltaTime)
                {
                    timer += deltaTime * timeScale;

                    while (timer >= 1.0f)
                    {
                        timer -= 1.0f;
                        Seconds++;

                        if (Seconds >= 60)
                        {
                            Seconds = 0;
                            AdvanceMinute();
                        }
                    }
                }
                private void AdvanceMinute()
                {
                    Minutes++;

                    if (Minutes >= 60)
                    {
                        Minutes = 0;
                        AdvanceHour();
                    }
                }
                private void AdvanceHour()
                {
                    Hours++;
                    OnHourPassed?.Invoke(Hours);

                    if (Hours >= 24)
                    {
                        Hours = 0;
                        AdvanceDay();
                    }
                }
                private void AdvanceDay()
                {
                    Day++;

                    int daysInCurrentMonth = useLunarMonths ? lunarMonthLength : daysPerCalendarMonth[Month - 1];
                    if (Day > daysInCurrentMonth)
                    {
                        Day = 1;
                        AdvanceMonth();
                    }
                    OnNewDay?.Invoke(Day);
                }
                private void AdvanceMonth()
                {
                    Month++;
                    if (Month > 12)
                    {
                        Month = 1;
                        AdvanceYear();
                    }
                    OnNewMonth?.Invoke(Month);
                }
                private void AdvanceYear()
                {
                    Year++;
                    OnNewYear?.Invoke(Year);
                }
                public string GetFormattedTime()
                {
                    return $"{Hours:D2}:{Minutes:D2}:{Seconds:D2}";
                }
                public string GetFormattedDate()
                {
                    string monthName = useLunarMonths ? $"Month {Month}" : monthNames[Month - 1];
                    return $"{monthName} {Day}, Year {Year}";
                }
            }
            public class Timer
            {
                private float duration;
                private float elapsedTime;
                private bool isRunning;
                private bool isRepeating;

                public event Action onTimerComplete;

                public Timer(float _duration, bool _isRepeating)
                {
                    duration = _duration;
                    isRepeating = _isRepeating;
                    elapsedTime = 0f;
                    isRunning = false;

                    //Empty Action
                    onTimerComplete += () => { };
                }
                public Timer(float _duration, bool _isRepeating, Action OnComplete)
                {
                    duration = _duration;
                    isRepeating = _isRepeating;
                    elapsedTime = 0f;
                    isRunning = false;

                    onTimerComplete += OnComplete;
                }
                public void Start(bool restart)
                {
                    isRunning = true;
                }
                public void Stop()
                {
                    isRunning = false;
                    elapsedTime = 0;
                }
                public void Pause()
                {
                    isRunning = false;
                }
                public void Resume()
                {
                    if (elapsedTime < duration)
                        isRunning = true;
                }
                public void Update(float deltaTime)
                {
                    if (!isRunning) return;

                    elapsedTime += deltaTime;
                    if (elapsedTime >= duration)
                    {
                        elapsedTime = isRepeating ? 0f : duration;
                        isRunning = isRepeating;
                        onTimerComplete?.Invoke();
                    }
                }
                public bool IsCompleted => elapsedTime >= duration;
                public float TimeRemaining => System.Math.Max(0, duration - elapsedTime);
                public float Progress => System.Math.Min(1, elapsedTime / duration);
            }
            public class TimeHelper
            {
                public static float SecondsToMilliseconds(float seconds)
                {
                    return seconds * 1000f;
                }
                public static float MillisecondsToSeconds(float milliseconds)
                {
                    return milliseconds / 1000f;
                }
                public static float ClampTime(float time, float min, float max)
                {
                    return MathHelper.Clamp(time, min, max);
                }
            }
        }
        namespace MathHelpers
        {
            public static class VectorHelper
            {
                public static float Magnitude(float x, float y)
                {
                    return (float)Math.Sqrt(x * x + y * y);
                }
                public static (float x, float y) Normalize(float x,  float y)
                {
                    float magnitude = Magnitude(x, y);

                    if (magnitude == 0)
                        return (0, 0);

                    return (x / magnitude, y / magnitude);
                }
                public static float DotProduct(float x1, float y1, float x2, float y2)
                {
                    return x1 * x2 + y1 * y2;
                }
                public static (float x, float y) Reflect(float x, float y, float normalX, float normalY)
                {
                    float dotProduct = DotProduct(x, y, normalX, normalY);
                    return (x - 2 * dotProduct * normalX, y - 2 * dotProduct * normalY);
                }
            }
            public static class Vector2Helper
            {
                public static float Distance(Vector2 vectorA, Vector2 vectorB)
                {
                    return Vector2.Distance(vectorA, vectorB);
                }
                public static float Angle(Vector2 from, Vector2 to)
                {
                    return (float)Math.Atan2(to.Y - from.Y, to.X - from.X);
                }
                public static Vector2 Normalize(Vector2 vector)
                {
                    return Vector2.Normalize(vector);
                }
                public static Vector2 Clamp(Vector2 vector, float min, float max)
                {
                    float length = vector.Length();

                    if (length < min)
                        return vector * (min / length);
                    else if (length > max)
                        return vector * (max / length);

                    return vector;
                }
                public static Vector2 Rotate(Vector2 vector, float radians)
                {
                    float cos = (float)Math.Cos(radians);
                    float sin = (float)Math.Sin(radians);
                    return new Vector2(vector.X * cos - vector.Y * sin, vector.X * sin + vector.Y * cos);
                }
            }
            public static class Vector3Helper
            {
                public static float Distance(Vector3 vectorA, Vector3 vectorB)
                {
                    return Vector3.Distance(vectorA, vectorB);
                }
                public static Vector3 Normalize(Vector3 vector)
                {
                    return Vector3.Normalize(vector);
                }
                public static Vector3 Clamp(Vector3 vector, float min, float max)
                {
                    float length = vector.Length();

                    if (length < min)
                        return vector * (min / length);
                    else if (length > max)
                        return vector * (max / length);

                    return vector;
                }
                public static Vector3 Cross(Vector3 vectorA, Vector3 vectorB)
                {
                    return Vector3.Cross(vectorA, vectorB);
                }
                public static Vector3 Rotate(Vector3 vector, Vector3 axis, float radians)
                {
                    return Vector3.Transform(vector, Matrix.CreateFromAxisAngle(axis, radians));
                }
            }
            public static class RandomHelper
            {
                private static readonly Random random = new Random();
                public static int RandomRange(int min, int max)
                {
                    return random.Next(min, max);
                }
                public static float RandomRange(float min, float max)
                {
                    return (float)(random.NextDouble() * (max - min) + min);
                }
                public static Vector2 RandomVector2(float min, float max)
                {
                    float angle = (float)(random.NextDouble() * Math.PI * 2);
                    float length = RandomRange(min, max);
                    return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * length;
                }
                public static Vector3 RandomVector3(float min, float max)
                {
                    float theta = (float)(random.NextDouble() * Math.PI * 2);
                    float phi = (float)(random.NextDouble() * Math.PI);
                    float length = RandomRange(min, max);

                    float x = length * (float)(Math.Sin(phi) * Math.Cos(theta));
                    float y = length * (float)(Math.Sin(phi) * Math.Sin(theta));
                    float z = length * (float)Math.Cos(phi);

                    return new Vector3(x, y, z);
                }
            }
            public static class LerpHelper
            {
                public static float Lerp(float start, float end, float deltaSpeed)
                {
                    return MathHelper.Lerp(start, end, deltaSpeed);
                }
                public static Vector2 Lerp(Vector2 start, Vector2 end, float deltaSpeed)
                {
                    return Vector2.Lerp(start, end, deltaSpeed);
                }
                public static float SmoothStep(float start, float end, float deltaSpeed)
                {
                    return MathHelper.SmoothStep(start, end, deltaSpeed);
                }
            }
            public static class GeometryHelper
            {
                public static bool IsPointInCircle(Vector2 point, Vector2 circleCenter, float radius)
                {
                    return Vector2.DistanceSquared(point, circleCenter) <= radius * radius;
                }
                public static bool LineIntersecting(Vector2 lineApointA, Vector2 lineApointB, Vector2 lineBpointA, Vector2 lineBpointB, out Vector2 intersection)
                {
                    float A1 = lineApointB.Y - lineApointA.Y;
                    float B1 = lineApointA.X - lineApointB.X;
                    float C1 = A1 * lineApointA.X + B1 * lineApointA.Y;

                    float A2 = lineBpointB.Y - lineBpointA.Y;
                    float B2 = lineBpointA.X - lineBpointB.X;
                    float C2 = A2 * lineBpointA.X + B1 * lineBpointA.Y;

                    float denominator = A1 * B2 - A2 * B1;

                    if (denominator == 0)
                    {
                        intersection = Vector2.Zero;
                        return false;
                    }

                    intersection = new Vector2((B2 * C1 - B1 * C2) / denominator, (A1 * C2 - A2 * C1) / denominator);
                    return true;
                }
            }
            public static class TrigonometryHelper
            {
                public static float ToRadians(float degrees)
                {
                    return MathHelper.ToRadians(degrees);
                }
                public static float ToDegrees(float radians)
                {
                    return MathHelper.ToDegrees(radians);
                }
                public static float Normalize(float angle)
                {
                    while (angle < -MathHelper.Pi) angle += MathHelper.TwoPi;
                    while (angle > MathHelper.Pi) angle -= MathHelper.TwoPi;
                    return angle;
                }
                public static float AngleDifference(float angleA, float angleB)
                {
                    return Normalize(angleA - angleB);
                }
            }
        }
        namespace General
        {
            public static class StringHelper
            {
                public static string CutToLength(string value, int maxLength)
                {
                    if (string.IsNullOrEmpty(value))
                        return value;
                    return value.Length <= maxLength ? value : value.Substring(0, maxLength);
                }
                public static string PadLeft(string value, int padCount, char paddingCharacter = ' ')
                {
                    return value.PadLeft(padCount, paddingCharacter);
                }
                public static string PadRight(string value, int padCount, char paddingCharacter = ' ')
                {
                    return value.PadRight(padCount, paddingCharacter);
                }
                public static string JoinWithSeparator(string separator, params string[] values)
                {
                    return string.Join(separator, values);
                }
            }
            public static class ColorHelper
            {
                private static readonly Random random = new Random();

                public static Color RandomColor(bool randomAlpha)
                {
                    if (randomAlpha)
                        return new Color(random.Next(256), random.Next(256), random.Next(256), random.Next(256));
                    else
                        return new Color(random.Next(256), random.Next(256), random.Next(256));
                }
                public static Color BlendColors(Color colorA, Color colorB, float value)
                {
                    return Color.Lerp(colorA, colorB, value);
                }
                public static Color AdjustBrightness(Color color, float brightnessFactor)
                {
                    return new Color(
                        (int)(color.R * brightnessFactor),
                        (int)(color.G * brightnessFactor),
                        (int)(color.B * brightnessFactor),
                        color.A);
                }
                public static Color AdjustAlpha(Color color, float alphaFactor)
                {
                    return new Color(
                        (color.R),
                        (color.G),
                        (color.B),
                        color.A * alphaFactor);
                }
            }
            public class ObjectPool<T> where T : class
            {
                private readonly Stack<T> pool;
                private readonly Func<T> createObject;
                private readonly int capacity;

                public ObjectPool(Func<T> _createFunction, int initialCapacity, int maxCapacity)
                {
                    pool = new Stack<T>(initialCapacity);
                    createObject = _createFunction ?? throw new ArgumentNullException(nameof(createObject));
                    capacity = maxCapacity;

                    for (int i = 0; i < initialCapacity; i++)
                        pool.Push(createObject());
                }

                public T Get()
                {
                    return pool.Count > 0 ? pool.Pop() : createObject();
                }
                public void Release(T item)
                {
                    if (pool.Count < capacity)
                        pool.Push(item);
                }
                public int Count => pool.Count;
            }
        }
    }
}
