using System.Collections.Generic;
using GameCode.Spirits.Conversation.Data;
using UnityEngine;

namespace GameCode.Spirits.Conversation.Runtime
{
    /// <summary>
    /// Runtime playback state for a single active conversation.
    /// Created by the ConversationDirector when a conversation is selected and played.
    /// This is a pure C# class — not a ScriptableObject or MonoBehaviour.
    /// 
    /// Tracks which node is currently playing, which nodes have been visited,
    /// and the conversation's lifecycle state (playing, paused, interrupted, etc.).
    /// </summary>
    public class ConversationInstance
    {
        /// <summary>
        /// The authored conversation asset being played.
        /// </summary>
        public ConversationAsset Asset { get; }

        /// <summary>
        /// The node ID currently being played or about to be played.
        /// </summary>
        public int CurrentNodeId { get; set; }

        /// <summary>
        /// Time.time when this conversation started playing.
        /// </summary>
        public float StartTime { get; }

        /// <summary>
        /// Time.time when the last line was emitted to the Coordinator.
        /// </summary>
        public float LastLineTime { get; set; }

        /// <summary>
        /// Current lifecycle state of this conversation.
        /// </summary>
        public ConversationState State { get; set; }

        /// <summary>
        /// History of all node IDs that have been played during this instance.
        /// Used for tracking partial completion and preventing infinite loops in branching.
        /// </summary>
        public List<int> PlayedNodeIds { get; }

        /// <summary>
        /// Creates a new conversation instance ready for playback.
        /// </summary>
        public ConversationInstance(ConversationAsset asset)
        {
            Asset = asset;
            CurrentNodeId = asset.RootNodeId;
            StartTime = Time.time;
            LastLineTime = Time.time;
            State = ConversationState.Playing;
            PlayedNodeIds = new List<int>(asset.NodeCount);
        }

        /// <summary>
        /// Returns the current node being played, or null if the ID is invalid.
        /// </summary>
        public ConversationNode GetCurrentNode()
        {
            return Asset.GetNode(CurrentNodeId);
        }

        /// <summary>
        /// Records a node as played and updates the current node pointer.
        /// </summary>
        public void AdvanceToNode(int nextNodeId)
        {
            PlayedNodeIds.Add(CurrentNodeId);
            CurrentNodeId = nextNodeId;
            LastLineTime = Time.time;
        }

        /// <summary>
        /// Marks the current node as played without advancing (used for terminal nodes).
        /// </summary>
        public void MarkCurrentNodePlayed()
        {
            if (!PlayedNodeIds.Contains(CurrentNodeId))
            {
                PlayedNodeIds.Add(CurrentNodeId);
            }
        }

        /// <summary>
        /// Returns true if the given node has already been visited during this playback.
        /// Used to prevent infinite loops in conversations with circular references.
        /// </summary>
        public bool HasVisitedNode(int nodeId)
        {
            return PlayedNodeIds.Contains(nodeId);
        }

        /// <summary>
        /// Returns how many nodes have been played so far.
        /// </summary>
        public int NodesPlayed => PlayedNodeIds.Count;

        /// <summary>
        /// Returns how long this conversation has been running.
        /// </summary>
        public float ElapsedTime => Time.time - StartTime;
    }

    /// <summary>
    /// Lifecycle states for a conversation instance.
    /// </summary>
    public enum ConversationState
    {
        /// <summary>
        /// A line is currently being displayed or the system is between lines.
        /// </summary>
        Playing,

        /// <summary>
        /// Waiting for the SpiritDialogueCoordinator to signal that the current line
        /// has finished displaying.
        /// </summary>
        WaitingForLineFinish,

        /// <summary>
        /// A reply delay is active before the next line plays (natural pause between speakers).
        /// </summary>
        DelayBeforeNextLine,

        /// <summary>
        /// Temporarily paused by an external system (e.g., menu open, cutscene).
        /// Can be resumed.
        /// </summary>
        Paused,

        /// <summary>
        /// A higher-priority conversation interrupted this one.
        /// May be resumable if CanResumeAfterInterrupt is true.
        /// </summary>
        Interrupted,

        /// <summary>
        /// All nodes have been played or a terminal node was reached.
        /// Final state — instance should be recorded to history.
        /// </summary>
        Completed,

        /// <summary>
        /// Conversation was terminated before completing (by cancel trigger, exit condition, or non-resumable interrupt).
        /// Final state — instance should be recorded to history.
        /// </summary>
        Cancelled
    }
}
