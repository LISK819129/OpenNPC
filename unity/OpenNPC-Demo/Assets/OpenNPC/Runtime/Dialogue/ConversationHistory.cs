using System.Collections.Generic;

namespace OpenNPC.Dialogue
{
    /// <summary>
    /// Short-term memory for one NPC: the most recent turns, oldest first.
    /// Bounded so requests stay small. Long-term memory (summaries, facts about
    /// the player, relationships) is framework work behind the provider boundary.
    /// </summary>
    public sealed class ConversationHistory
    {
        private readonly List<ConversationTurn> _turns = new List<ConversationTurn>();
        private readonly int _maxTurns;

        public ConversationHistory(int maxTurns)
        {
            _maxTurns = maxTurns < 2 ? 2 : maxTurns;
        }

        public IReadOnlyList<ConversationTurn> Turns => _turns;
        public int Count => _turns.Count;

        /// <summary>Total turns ever recorded, including ones trimmed away.</summary>
        public int TotalRecorded { get; private set; }

        public void Add(ConversationRole role, string content)
        {
            if (string.IsNullOrEmpty(content))
                return;
            _turns.Add(new ConversationTurn(role, content));
            TotalRecorded++;
            while (_turns.Count > _maxTurns)
                _turns.RemoveAt(0);
        }

        /// <summary>A copy, so an in-flight request is not affected by later turns.</summary>
        public List<ConversationTurn> Snapshot() => new List<ConversationTurn>(_turns);

        public void Clear()
        {
            _turns.Clear();
        }
    }
}
