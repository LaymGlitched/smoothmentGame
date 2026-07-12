using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdvancedPrefabPainter.Runtime.Data
{
    [Serializable]
    public class PrefabItem
    {
        public GameObject prefab;
        [Range(0f, 1f)]
        public float weight = 1f;

        [Header("Scale")]
        public bool uniformScale = true;
        public Vector2 minMaxScaleX = new Vector2(0.8f, 1.2f);
        public Vector2 minMaxScaleY = new Vector2(0.8f, 1.2f);
        public Vector2 minMaxScaleZ = new Vector2(0.8f, 1.2f);

        [Header("Rotation")]
        public bool randomRotationY = true;
        public Vector3 randomRotationXYZ = Vector3.zero;
        public Vector3 offsetRotation = Vector3.zero;

        [Header("Collision")]
        public float collisionRadius = 0.5f;

        public Vector3 GetRandomScale()
        {
            if (uniformScale)
            {
                float s = UnityEngine.Random.Range(minMaxScaleX.x, minMaxScaleX.y);
                return new Vector3(s, s, s);
            }
            else
            {
                return new Vector3(
                    UnityEngine.Random.Range(minMaxScaleX.x, minMaxScaleX.y),
                    UnityEngine.Random.Range(minMaxScaleY.x, minMaxScaleY.y),
                    UnityEngine.Random.Range(minMaxScaleZ.x, minMaxScaleZ.y)
                );
            }
        }

        public Quaternion GetRandomRotation(Vector3 normal, float alignmentValue)
        {
            Quaternion rot = Quaternion.Euler(offsetRotation);

            if (randomRotationY)
            {
                rot *= Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
            }

            if (randomRotationXYZ.sqrMagnitude > 0)
            {
                rot *= Quaternion.Euler(
                    UnityEngine.Random.Range(-randomRotationXYZ.x, randomRotationXYZ.x),
                    UnityEngine.Random.Range(-randomRotationXYZ.y, randomRotationXYZ.y),
                    UnityEngine.Random.Range(-randomRotationXYZ.z, randomRotationXYZ.z)
                );
            }

            if (alignmentValue > 0)
            {
                Quaternion surfaceRot = Quaternion.FromToRotation(Vector3.up, normal);
                rot = Quaternion.Slerp(rot, surfaceRot * rot, alignmentValue);
            }

            return rot;
        }
    }

    public enum PlacementMode
    {
        Random,
        WeightedRandom,
        Sequential
    }

    [CreateAssetMenu(fileName = "New Prefab Palette", menuName = "Advanced Prefab Painter/Prefab Palette")]
    public class PrefabPalette : ScriptableObject
    {
        public List<PrefabItem> items = new List<PrefabItem>();
        public PlacementMode placementMode = PlacementMode.WeightedRandom;

        public PrefabItem GetNextItem(ref int currentIndex)
        {
            if (items == null || items.Count == 0) return null;

            switch (placementMode)
            {
                case PlacementMode.Sequential:
                    currentIndex = (currentIndex + 1) % items.Count;
                    return items[currentIndex];
                case PlacementMode.Random:
                    return items[UnityEngine.Random.Range(0, items.Count)];
                case PlacementMode.WeightedRandom:
                default:
                    float totalWeight = 0;
                    foreach (var item in items) totalWeight += item.weight;
                    float r = UnityEngine.Random.Range(0f, totalWeight);
                    float currentWeight = 0;
                    foreach (var item in items)
                    {
                        currentWeight += item.weight;
                        if (r <= currentWeight) return item;
                    }
                    return items[0];
            }
        }
    }
}
