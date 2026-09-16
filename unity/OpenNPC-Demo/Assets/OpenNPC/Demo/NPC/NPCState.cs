namespace OpenNPC.Demo.NPC
{
    public enum NPCState
    {
        /// <summary>Following its lane.</summary>
        Walking,
        /// <summary>Standing still for a while (looking around).</summary>
        Idle,
        /// <summary>In a conversation with the player.</summary>
        Talking,
        /// <summary>Walking off after a conversation, or off the end of the street.</summary>
        Leaving,
    }
}
