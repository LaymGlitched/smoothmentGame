using System;
using UnityEngine;

namespace Reiteki.Localization.Core
{
    [Serializable]
    public struct LocalizationKey : IEquatable<LocalizationKey>
    {
        public string Value;
        
        public LocalizationKey(string value) => Value = value;
        
        public bool Equals(LocalizationKey other) => string.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is LocalizationKey other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public static bool operator ==(LocalizationKey left, LocalizationKey right) => left.Equals(right);
        public static bool operator !=(LocalizationKey left, LocalizationKey right) => !left.Equals(right);
        public override string ToString() => Value ?? string.Empty;
        
        public static implicit operator string(LocalizationKey id) => id.Value;
        public static implicit operator LocalizationKey(string value) => new LocalizationKey(value);
    }
}
