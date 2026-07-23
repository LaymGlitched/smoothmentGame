using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Zero-allocation binary serialization helpers for Unity types.
    /// All methods operate on BinaryWriter/BinaryReader over pooled streams.
    /// </summary>
    public static class Serialization
    {
        // --- Vector3 ---

        public static void WriteVector3(BinaryWriter w, Vector3 v)
        {
            w.Write(v.x);
            w.Write(v.y);
            w.Write(v.z);
        }

        public static Vector3 ReadVector3(BinaryReader r)
        {
            return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        // --- Quaternion ---

        public static void WriteQuaternion(BinaryWriter w, Quaternion q)
        {
            w.Write(q.x);
            w.Write(q.y);
            w.Write(q.z);
            w.Write(q.w);
        }

        public static Quaternion ReadQuaternion(BinaryReader r)
        {
            return new Quaternion(
                r.ReadSingle(), r.ReadSingle(),
                r.ReadSingle(), r.ReadSingle());
        }

        // --- Guid ---

        public static void WriteGuid(BinaryWriter w, Guid g)
        {
            w.Write(g.ToByteArray());
        }

        public static Guid ReadGuid(BinaryReader r)
        {
            return new Guid(r.ReadBytes(16));
        }

        // --- String (length-prefixed UTF-8) ---

        public static void WriteString(BinaryWriter w, string s)
        {
            if (s == null) s = "";
            var bytes = Encoding.UTF8.GetBytes(s);
            w.Write((ushort)bytes.Length);
            w.Write(bytes);
        }

        public static string ReadString(BinaryReader r)
        {
            int len = r.ReadUInt16();
            if (len == 0) return "";
            return Encoding.UTF8.GetString(r.ReadBytes(len));
        }

        // --- Color ---

        public static void WriteColor(BinaryWriter w, Color c)
        {
            w.Write(c.r);
            w.Write(c.g);
            w.Write(c.b);
            w.Write(c.a);
        }

        public static Color ReadColor(BinaryReader r)
        {
            return new Color(
                r.ReadSingle(), r.ReadSingle(),
                r.ReadSingle(), r.ReadSingle());
        }

        // --- Framed message helpers ---

        /// <summary>
        /// Writes a length-framed message: [ushort length][byte msgType][payload].
        /// Returns the complete frame as a byte array.
        /// </summary>
        public static byte[] Frame(MsgType type, byte[] payload)
        {
            // length = 1 (type) + payload.Length
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
