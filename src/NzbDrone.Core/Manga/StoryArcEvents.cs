using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Manga
{
    public class StoryArcAddedEvent : IEvent
    {
        public StoryArc Arc { get; set; }

        public StoryArcAddedEvent(StoryArc arc)
        {
            Arc = arc;
        }
    }

    public class StoryArcUpdatedEvent : IEvent
    {
        public StoryArc Arc { get; set; }

        public StoryArcUpdatedEvent(StoryArc arc)
        {
            Arc = arc;
        }
    }

    public class StoryArcDeletedEvent : IEvent
    {
        public StoryArc Arc { get; set; }

        public StoryArcDeletedEvent(StoryArc arc)
        {
            Arc = arc;
        }
    }
}
