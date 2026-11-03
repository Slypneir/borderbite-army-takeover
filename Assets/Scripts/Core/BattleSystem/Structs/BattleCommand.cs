using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// From the given tick the hero of a side walks in a direction, (0, 0) stops it: the only way anything enters the simulation.
    /// The direction is any vector of integers up to <see cref="MaxAxis"/>; its length does not matter, the speed is constant.
    /// </summary>
    public readonly struct BattleCommand
    {
        public const int MaxAxis = 1000;

        public readonly int Tick;
        public readonly Side Side;
        public readonly int DirectionX;
        public readonly int DirectionY;

        public BattleCommand(int tick, Side side, int directionX, int directionY)
        {
            Tick = tick;
            Side = side;
            DirectionX = directionX;
            DirectionY = directionY;
        }
    }
}
