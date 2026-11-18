using System;
using System.Collections.Generic;
using UnityEngine;

namespace AFrame.Unity
{
    public abstract class AFrameSceneEntry : MonoBehaviour
    {
        public virtual IReadOnlyList<string> Features => Array.Empty<string>();
        public virtual GameDimension? DimensionOverride => null;

        public AFrameBootstrap Host { get; private set; }

        internal void Attach(AFrameBootstrap host)
        {
            Host = host;
            Enter(host);
        }

        public abstract void Enter(AFrameBootstrap bootstrap);
        public virtual void SampleInput() { }
    }
}
