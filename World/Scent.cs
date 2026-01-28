using System;
using LivingSim.Core; // <--- ADDED: This allows access to AnimalType

namespace LivingSim.World
{
    /// <summary>
    /// Represents a scent left by an animal in a world cell.
    /// </summary>
    public class Scent
    {
        public Guid GroupId { get; }
        public AnimalType Type { get; }
        public float Strength { get; set; }

        public Scent(Guid groupId, AnimalType type, float initialStrength)
        {
            GroupId = groupId;
            Type = type;
            Strength = initialStrength;
        }
    }
}