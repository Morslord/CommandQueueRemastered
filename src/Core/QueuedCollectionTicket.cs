namespace CommandQueueRemastered.Core
{
    // A remote hint may authorize only the immediately following matching selection.
    public sealed class QueuedCollectionTicket
    {
        private readonly long participant;
        private readonly int option;
        private readonly float expires;
        private bool consumed;
        public QueuedCollectionTicket(long participant, int option, float expires)
        { this.participant = participant; this.option = option; this.expires = expires; }
        public bool TryConsume(long currentParticipant, int selectedOption, float now)
        {
            if (consumed) return false;
            consumed = true;
            return participant == currentParticipant && option == selectedOption && now <= expires;
        }
    }
}
