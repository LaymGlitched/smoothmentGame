using System;
using UnityEngine;

namespace GameCode.Spirits.Data
{
    [Serializable]
    public struct TopicId : IEquatable<TopicId>
    {
        public string Value;
        
        public TopicId(string value) => Value = value;
        
        public bool Equals(TopicId other) => string.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is TopicId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public static bool operator ==(TopicId left, TopicId right) => left.Equals(right);
        public static bool operator !=(TopicId left, TopicId right) => !left.Equals(right);
        public override string ToString() => Value ?? string.Empty;
        
        public static implicit operator string(TopicId id) => id.Value;
        public static implicit operator TopicId(string value) => new TopicId(value);
    }

    [Serializable]
    public struct EventId : IEquatable<EventId>
    {
        public string Value;
        
        public EventId(string value) => Value = value;
        
        public bool Equals(EventId other) => string.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is EventId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public static bool operator ==(EventId left, EventId right) => left.Equals(right);
        public static bool operator !=(EventId left, EventId right) => !left.Equals(right);
        public override string ToString() => Value ?? string.Empty;
        
        public static implicit operator string(EventId id) => id.Value;
        public static implicit operator EventId(string value) => new EventId(value);
    }

    [Serializable]
    public struct SpiritId : IEquatable<SpiritId>
    {
        public string Value;
        
        public SpiritId(string value) => Value = value;
        
        public bool Equals(SpiritId other) => string.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is SpiritId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public static bool operator ==(SpiritId left, SpiritId right) => left.Equals(right);
        public static bool operator !=(SpiritId left, SpiritId right) => !left.Equals(right);
        public override string ToString() => Value ?? string.Empty;
        
        public static implicit operator string(SpiritId id) => id.Value;
        public static implicit operator SpiritId(string value) => new SpiritId(value);
    }

    [Serializable]
    public struct ScenarioId : IEquatable<ScenarioId>
    {
        public string Value;
        
        public ScenarioId(string value) => Value = value;
        
        public bool Equals(ScenarioId other) => string.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is ScenarioId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public static bool operator ==(ScenarioId left, ScenarioId right) => left.Equals(right);
        public static bool operator !=(ScenarioId left, ScenarioId right) => !left.Equals(right);
        public override string ToString() => Value ?? string.Empty;
        
        public static implicit operator string(ScenarioId id) => id.Value;
        public static implicit operator ScenarioId(string value) => new ScenarioId(value);
    }

    [Serializable]
    public struct ConcernId : IEquatable<ConcernId>
    {
        public string Value;
        
        public ConcernId(string value) => Value = value;
        
        public bool Equals(ConcernId other) => string.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is ConcernId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public static bool operator ==(ConcernId left, ConcernId right) => left.Equals(right);
        public static bool operator !=(ConcernId left, ConcernId right) => !left.Equals(right);
        public override string ToString() => Value ?? string.Empty;
        
        public static implicit operator string(ConcernId id) => id.Value;
        public static implicit operator ConcernId(string value) => new ConcernId(value);
    }
}
