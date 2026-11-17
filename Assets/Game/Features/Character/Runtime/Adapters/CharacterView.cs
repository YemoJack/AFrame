using AFrame.Unity;
using UnityEngine;

namespace AFrame.Game.Character
{
    public sealed class CharacterView : AFrameView
    {
        [SerializeField] private GameDimension dimension;
        private ICharacterState query;
        private Vector3 previous;
        private Vector3 current;

        public override void Bind(IViewContext context)
        {
            query = context.Query<ICharacterState>();
            OnTickCompleted();
            previous = current;
            transform.position = current;
        }

        public override void Unbind() => query = null;

        public override void OnTickCompleted()
        {
            previous = current;
            var position = query.Position;
            current = dimension == GameDimension.TwoD
                ? new Vector3(position.x, position.z, 0f)
                : new Vector3(position.x, 0f, position.z);
        }

        public override void OnRender(float alpha) => transform.position = Vector3.Lerp(previous, current, alpha);
    }
}
