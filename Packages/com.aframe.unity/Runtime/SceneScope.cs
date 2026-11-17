using System;
using System.Collections.Generic;
using AFrame.Runtime;
using UnityEngine;

namespace AFrame.Unity
{
    [DisallowMultipleComponent]
    public sealed class SceneScope : MonoBehaviour
    {
        private readonly List<(AFrameView View, ViewContext Context)> bindings =
            new List<(AFrameView, ViewContext)>();
        private ModuleContext session;
        public AFrameBootstrap Host { get; internal set; }

        internal void Bind(ModuleContext context)
        {
            Release();
            session = context;
            if (isActiveAndEnabled)
                OnEnable();
        }

        private void OnEnable()
        {
            if (session == null)
                return;
            foreach (var view in GetComponentsInChildren<AFrameView>(true))
            {
                var context = new ViewContext(session);
                try { view.Bind(context); }
                catch { context.Dispose(); Release(); throw; }
                bindings.Add((view, context));
            }
        }

        internal void TickCompleted()
        {
            foreach (var binding in bindings)
                if (binding.View != null && binding.View.isActiveAndEnabled)
                    binding.View.OnTickCompleted();
        }

        internal void Render(float alpha)
        {
            foreach (var binding in bindings)
                if (binding.View != null && binding.View.isActiveAndEnabled)
                    binding.View.OnRender(alpha);
        }

        private void OnDisable()
        {
            foreach (var binding in bindings)
            {
                try { if (binding.View != null) binding.View.Unbind(); }
                catch (Exception error) { Debug.LogException(error, this); }
                finally { binding.Context.Dispose(); }
            }
            bindings.Clear();
        }

        internal void Release()
        {
            OnDisable();
            session = null;
        }
    }
}
