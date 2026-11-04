using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// The only way anything enters the simulation. A walk: from the given tick the hero of a side walks in a direction, (0, 0)
    /// stops it; the direction is any vector of integers up to <see cref="MaxAxis"/>, its length does not matter, the speed is
    /// constant. An order (<see cref="IsOrder"/>): from the given tick the army of the side follows <see cref="Order"/>; the same
    /// order again takes it back to the free attack (spec 3.8, T17).
    /// </summary>
    public readonly struct BattleCommand
    {
        public const int MaxAxis = 1000;

        public readonly int Tick;
        public readonly Side Side;
        public readonly int DirectionX;
        public readonly int DirectionY;
        public readonly bool IsOrder;
        public readonly ArmyOrder Order;

        public BattleCommand(int tick, Side side, int directionX, int directionY)
        {
            Tick = tick;
            Side = side;
            DirectionX = directionX;
            DirectionY = directionY;
            IsOrder = false;
            Order = ArmyOrder.Free;
        }

        public BattleCommand(int tick, Side side, ArmyOrder order)
        {
            Tick = tick;
            Side = side;
            DirectionX = 0;
            DirectionY = 0;
            IsOrder = true;
            Order = order;
        }
    }
}
