using System;
using UnityEngine;

namespace GameCode.Spirits.Conversation.Data
{
    /// <summary>
    /// Type-safe identifier for a conversation asset.
    /// </summary>
    [Serializable]
    public struct ConversationId : IEquatable<ConversationId>
    {
        public string Value;

        public ConversationId(string value) => Value = value;

        public bool Equals(ConversationId other) => string.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is ConversationId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public static bool operator ==(ConversationId left, ConversationId right) => left.Equals(right);
        public static bool operator !=(ConversationId left, ConversationId right) => !left.Equals(right);
        public override string ToString() => Value ?? string.Empty;

        public static implicit operator string(ConversationId id) => id.Value;
        public static implicit operator ConversationId(string value) => new ConversationId(value);
    }

    /// <summary>
    /// Type-safe identifier for a conversation trigger signal.
    /// Maps gameplay events to conversation candidates. Multiple conversations
    /// can share the same trigger, enabling scored selection at runtime.
    /// </summary>
    [Serializable]
    public struct ConversationTrigger : IEquatable<ConversationTrigger>
    {
        public string Value;

        public ConversationTrigger(string value) => Value = value;

        public bool Equals(ConversationTrigger other) => string.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is ConversationTrigger other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public static bool operator ==(ConversationTrigger left, ConversationTrigger right) => left.Equals(right);
        public static bool operator !=(ConversationTrigger left, ConversationTrigger right) => !left.Equals(right);
        public override string ToString() => Value ?? string.Empty;

        public static implicit operator string(ConversationTrigger id) => id.Value;
        public static implicit operator ConversationTrigger(string value) => new ConversationTrigger(value);
    }
}
