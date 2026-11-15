using AFrame.Foundation;

namespace AFrame.Physics
{
    public readonly struct PhysicsHit
    {
        public PhysicsHit(int entityId, Float3 point, Float3 normal)
        {
            EntityId = entityId;
            Point = point;
            Normal = normal;
        }

        public int EntityId { get; }
        public Float3 Point { get; }
        public Float3 Normal { get; }
    }

    public interface IPhysicsQuery
    {
        bool TryMove(int entityId, Float3 delta, out PhysicsHit hit);
    }
}
