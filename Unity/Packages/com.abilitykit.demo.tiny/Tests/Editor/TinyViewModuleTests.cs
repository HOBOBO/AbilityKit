using System;
using System.Collections.Generic;
using System.Threading;
using AbilityKit.Demo.Tiny.View;
using AbilityKit.Game.View.Modules;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Demo.Tiny.Tests
{
    public sealed class TinyViewModuleTests
    {
        [Test]
        public void AttachDetachAndReattachOwnSceneObjects()
        {
            var root = new GameObject("Tiny Module Test");
            try
            {
                var context = new TinyViewModuleContext(root.transform, () => null,
                    CancellationToken.None, exception => throw exception);
                var host = new ModuleHost<TinyViewModuleContext, IGameModule<TinyViewModuleContext>>(
                    new List<IGameModule<TinyViewModuleContext>>
                    {
                        new TinyActorViewModule(),
                        new TinyInputModule()
                    });
                Assert.That(host.TrySortByDependencies(), Is.True);

                host.Attach(in context);
                Assert.That(root.transform.childCount, Is.EqualTo(3));
                host.Tick(in context, 0.016f);
                host.Detach(in context);
                Assert.That(root.transform.childCount, Is.Zero);

                host.Attach(in context);
                Assert.That(root.transform.childCount, Is.EqualTo(3));
                host.Detach(in context);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
