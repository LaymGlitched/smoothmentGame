using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// NanoCollab message type headers. Single byte on the wire.
    /// </summary>
    public enum MsgType : byte
    {
        // Presence (0x01–0x0F)
        UserJoin        = 0x01,
        UserLeave       = 0x02,
        UserList        = 0x03,

        // Sync (0x10–0x1F)
        TransformUpdate = 0x10,
        HierarchyChange = 0x11,
        SelectionChange = 0x12,
        CameraUpdate    = 0x13,
        Manipulation    = 0x14, // Object drag start/stop awareness

        // System (0xFE–0xFF)
        Ping            = 0xFE,
        Pong            = 0xFF,
    }

    /// <summary>
    /// Hierarchy change sub-types.
    /// </summary>
    public enum HierarchyChangeType : byte
    {
        Reparent = 0x01,
        Rename   = 0x02,
        Create   = 0x03,
        Delete   = 0x04,
    }

    /// <summary>
    /// Binary serialization extension methods for Unity & NanoCollab types.
    /// Operates directly on BinaryWriter / BinaryReader.
    /// </summary>
    public static class SerializationExtensions
    {
        // --- Vector3 ---

        public static void WriteVector3(this BinaryWriter w, Vector3 v)
        {
            w.Write(v.x);
            w.Write(v.y);
            w.Write(v.z);
        }

        public static Vector3 ReadVector3(this BinaryReader r)
        {
            return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        // --- Quaternion ---

        public static void WriteQuaternion(this BinaryWriter w, Quaternion q)
        {
            w.Write(q.x);
            w.Write(q.y);
            w.Write(q.z);
            w.Write(q.w);
        }

        public static Quaternion ReadQuaternion(this BinaryReader r)
        {
            return new Quaternion(
                r.ReadSingle(), r.ReadSingle(),
                r.ReadSingle(), r.ReadSingle());
        }

        // --- Guid ---

        public static void WriteGuid(this BinaryWriter w, Guid g)
        {
            w.Write(g.ToByteArray());
        }

        public static Guid ReadGuid(this BinaryReader r)
        {
            return new Guid(r.ReadBytes(16));
        }

        // --- String (length-prefixed UTF-8) ---

        public static void WriteString(this BinaryWriter w, string s)
        {
            if (s == null) s = "";
            var bytes = Encoding.UTF8.GetBytes(s);
            w.Write((ushort)bytes.Length);
            w.Write(bytes);
        }

        public static string ReadString(this BinaryReader r)
        {
            int len = r.ReadUInt16();
            if (len == 0) return "";
            return Encoding.UTF8.GetString(r.ReadBytes(len));
        }

        // --- GlobalObjectId ---

        public static void WriteGlobalObjectId(this BinaryWriter w, GlobalObjectId id)
        {
            w.WriteString(id.ToString());
        }

        public static GlobalObjectId ReadGlobalObjectId(this BinaryReader r)
        {
            string s = r.ReadString();
            if (string.IsNullOrEmpty(s)) return default;
            GlobalObjectId.TryParse(s, out var id);
            return id;
        }

        public static GameObject ToGameObject(this GlobalObjectId id)
        {
            if (id.Equals(default(GlobalObjectId))) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
        }

        public static bool IsValid(this GlobalObjectId id)
        {
            return !id.Equals(default(GlobalObjectId));
        }

        // --- Color ---

        public static void WriteColor(this BinaryWriter w, Color c)
        {
            w.Write(c.r);
            w.Write(c.g);
            w.Write(c.b);
            w.Write(c.a);
        }

        public static Color ReadColor(this BinaryReader r)
        {
            return new Color(
                r.ReadSingle(), r.ReadSingle(),
                r.ReadSingle(), r.ReadSingle());
        }

        // --- Framed Message Helper ---

        public static byte[] FrameMessage(MsgType type, byte[] payload)
        {
            int payloadLen = payload?.Length ?? 0;
            var frame = new byte[2 + 1 + payloadLen];
            ushort len = (ushort)(1 + payloadLen);
            frame[0] = (byte)(len & 0xFF);
            frame[1] = (byte)(len >> 8);
            frame[2] = (byte)type;
            if (payloadLen > 0)
                Buffer.BlockCopy(payload, 0, frame, 3, payloadLen);
            return frame;
        }
    }
}
