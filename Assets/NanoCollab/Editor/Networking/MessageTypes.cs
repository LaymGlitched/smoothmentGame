namespace NanoCollab
{
    /// <summary>
    /// All NanoCollab message types. Single byte on the wire.
    /// </summary>
    public enum MsgType : byte
    {
        // Presence (0x01–0x0F)
        UserJoin      = 0x01,
        UserLeave     = 0x02,
        UserList      = 0x03,

        // Sync (0x10–0x1F)
        TransformUpdate  = 0x10,
        HierarchyChange  = 0x11,
        SelectionChange  = 0x12,
        CameraUpdate     = 0x13,

        // System (0xFE–0xFF)
        Ping = 0xFE,
        Pong = 0xFF,
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
}
