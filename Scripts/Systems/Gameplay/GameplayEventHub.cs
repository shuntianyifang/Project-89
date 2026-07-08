using System;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameplayEventHub
    {
        public event Action<GameplayEvent> EventPublished;

        public void Publish(GameplayEvent gameplayEvent)
        {
            EventPublished?.Invoke(gameplayEvent);
        }
    }
}
