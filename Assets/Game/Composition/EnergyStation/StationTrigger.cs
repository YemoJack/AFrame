using System;
using UnityEngine;

namespace AFrame.Game.EnergyStation
{
    internal sealed class StationTrigger : MonoBehaviour
    {
        private Collider player;
        private Action onContact;

        public void Bind(Collider playerCollider, Action callback)
        {
            player = playerCollider;
            onContact = callback;
        }

        private void OnTriggerEnter(Collider other) => Respond(other);
        private void OnTriggerStay(Collider other) => Respond(other);

        private void Respond(Collider other)
        {
            if (other == player)
                onContact?.Invoke();
        }
    }
}
