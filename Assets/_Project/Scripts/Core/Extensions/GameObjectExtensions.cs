using UnityEngine;

namespace CZ.Core.Extensions
{
    public static class GameObjectExtensions
    {
        public static bool IsInLayerMask(this GameObject gameObject, LayerMask layerMask)
        {
            return ((1 << gameObject.layer) & layerMask) != 0;
        }
    }
} 