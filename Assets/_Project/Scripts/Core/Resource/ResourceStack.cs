using UnityEngine;
using System;
using System.Collections.Generic;

namespace CZ.Core.Resource
{
    public class ResourceStack
    {
        #region Properties
        public ResourceType ResourceType { get; private set; }
        public int CurrentStackSize => resources.Count;
        public int MaxStackSize { get; private set; }
        public bool IsFull => CurrentStackSize >= MaxStackSize;
        #endregion

        #region Events
        public event Action<ResourceType, int> OnStackCompleted;
        public event Action<ResourceType, int> OnResourceAdded;
        #endregion

        #region State
        private readonly List<BaseResource> resources;
        private readonly ResourceConfiguration config;
        #endregion

        public ResourceStack(ResourceType type, ResourceConfiguration configuration)
        {
            ResourceType = type;
            config = configuration;
            resources = new List<BaseResource>();

            // Set max stack size based on resource type
            MaxStackSize = type switch
            {
                ResourceType.Health => config.healthStackSize,
                ResourceType.Experience => config.experienceStackSize,
                ResourceType.Currency => config.currencyStackSize,
                _ => 1 // PowerUps don't stack
            };
        }

        #region Stack Management
        public bool TryAddResource(BaseResource resource)
        {
            if (resource == null || resource.ResourceType != ResourceType || IsFull)
                return false;

            resources.Add(resource);
            OnResourceAdded?.Invoke(ResourceType, resource.ResourceValue);

            if (IsFull)
            {
                CompleteStack();
            }

            return true;
        }

        private void CompleteStack()
        {
            int totalValue = 0;
            foreach (var resource in resources)
            {
                totalValue += resource.ResourceValue;
            }

            // Apply stack bonus
            totalValue *= config.CalculateStackBonus(CurrentStackSize);

            // Notify listeners
            OnStackCompleted?.Invoke(ResourceType, totalValue);

            // Clear the stack
            resources.Clear();
        }
        #endregion

        #region Stack Information
        public float GetCurrentCollectionRadius()
        {
            return config.GetStackCollectionRadius(CurrentStackSize);
        }

        public void Clear()
        {
            resources.Clear();
        }
        #endregion
    }
} 