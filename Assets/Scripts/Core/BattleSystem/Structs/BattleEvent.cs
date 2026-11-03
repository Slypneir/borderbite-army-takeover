using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// Something the drawing and the sounds show once: a pickup, a shot, a death, a building. Positions in units; the meaning of
    /// <see cref="Id"/>, <see cref="Value"/> and <see cref="Detail"/> depends on <see cref="Kind"/> (a resource or soldier id, a
    /// site index; an amount; the kind of a resource).
    /// </summary>
    public readonly struct BattleEvent
    {
        public readonly int Tick;
        public readonly EventKind Kind;
        public readonly Side Side;
        public readonly int Id;
        public readonly int Value;
        public readonly int X;
        public readonly int Y;
        public readonly int ToX;
        public readonly int ToY;
        public readonly int Detail;

        public BattleEvent(int tick, EventKind kind, Side side, int id, int value, int x, int y, int toX = 0, int toY = 0, int detail = 0)
        {
            Tick = tick;
            Kind = kind;
            Side = side;
            Id = id;
            Value = value;
            X = x;
            Y = y;
            ToX = toX;
            ToY = toY;
            Detail = detail;
        }
    }
}
