using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    internal static class sfUnity6Helper
    {
        public static int GetId(this UObject obj)
        {
            if (obj == null)
            {
                return 0;
            }
#if UNITY_6000_0_OR_NEWER
            return obj.GetEntityId().ToInt();
#else
            return obj.GetInstanceID();
#endif
        }

#if UNITY_6000_0_OR_NEWER
        private static FieldInfo m_entityIdField;
        private static ConstructorInfo m_entityIdCtor;

        static sfUnity6Helper()
        {
            Type type = typeof(EntityId);
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fields.Length > 0)
            {
                m_entityIdField = fields[0];
            }

            ConstructorInfo[] ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (ConstructorInfo c in ctors)
            {
                ParameterInfo[] p = c.GetParameters();
                if (p.Length == 1 && (p[0].ParameterType == typeof(int) || p[0].ParameterType == typeof(uint) || 
                                     p[0].ParameterType == typeof(ulong) || p[0].ParameterType == typeof(long)))
                {
                    m_entityIdCtor = c;
                    break;
                }
            }
        }

        public static int ToInt(this EntityId entityId)
        {
            if (m_entityIdField != null)
            {
                object val = m_entityIdField.GetValue(entityId);
                if (val != null)
                {
                    try
                    {
                        return Convert.ToInt32(val);
                    }
                    catch {}
                }
            }
            return entityId.GetHashCode();
        }

        public static EntityId ToEntityId(this int instanceId)
        {
            if (m_entityIdCtor != null)
            {
                try
                {
                    ParameterInfo[] p = m_entityIdCtor.GetParameters();
                    object arg = Convert.ChangeType(instanceId, p[0].ParameterType);
                    return (EntityId)m_entityIdCtor.Invoke(new object[] { arg });
                }
                catch {}
            }

            object boxed = default(EntityId);
            if (m_entityIdField != null)
            {
                try
                {
                    object val = Convert.ChangeType(instanceId, m_entityIdField.FieldType);
                    m_entityIdField.SetValue(boxed, val);
                }
                catch {}
            }
            return (EntityId)boxed;
        }
#endif
    }
}
