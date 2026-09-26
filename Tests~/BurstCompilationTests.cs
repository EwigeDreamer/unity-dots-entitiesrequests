using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Unity.Burst;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Tests
{
    /// <summary>Stages of a canary system, used as the result flags of the Burst probe.</summary>
    [Flags]
    public enum CanaryStages : byte
    {
        /// <summary>No stage.</summary>
        None = 0,

        /// <summary>The <c>OnCreate</c> stage.</summary>
        OnCreate = 1,

        /// <summary>The <c>OnUpdate</c> stage.</summary>
        OnUpdate = 2,

        /// <summary>The <c>OnDestroy</c> stage.</summary>
        OnDestroy = 4,
    }

    /// <summary>
    /// Runtime probe telling whether the calling code is Burst compiled. Uses the canonical
    /// <see cref="BurstDiscardAttribute"/> trick: a method marked with it is removed at the call site
    /// when the caller is Burst compiled, so a flag initialized to <c>true</c> stays <c>true</c> only
    /// on the Burst path. Shared by the Burst and the managed canaries so that the two systems differ
    /// only by their <c>[BurstCompile]</c> attribute.
    /// </summary>
    public static class BurstProbe
    {
        /// <summary>Returns <c>true</c> when called from Burst-compiled code, <c>false</c> from managed.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBurst()
        {
            var isBurst = true;
            SetManagedIfNotBurst(ref isBurst);
            return isBurst;
        }

        /// <summary>Runs only on the managed path; the call is discarded when Burst compiled.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstDiscard]
        private static void SetManagedIfNotBurst(ref bool isBurst) => isBurst = false;
    }

    /// <summary>
    /// Burst-compiled canary: every stage must mark its flag. The other unmanaged fixtures only verify
    /// behavior, so they stay green even when Burst is disabled or silently falls back to managed;
    /// this file turns "ran under Burst" into an assertion.
    /// <para>
    /// Entities generates a separate Burst entry point per stage (<c>__codegen__OnCreate</c>,
    /// <c>__codegen__OnUpdate</c>, <c>__codegen__OnDestroy</c>) and transfers <c>[BurstCompile]</c>
    /// from the method onto that wrapper (<c>ISystemPostProcessor.cs:188-198</c>), so each stage is
    /// probed on its own.
    /// </para>
    /// <para>
    /// Results are published through <see cref="SharedStatic{T}"/>: unlike a heap buffer it needs no
    /// owner and outlives the system, so the <c>OnDestroy</c> result can be read after the system is
    /// gone. Accessing the field from managed code also runs the static constructor, which is what
    /// initializes the shared buffer in C# before HPC# touches it (Burst docs, csharp-shared-static.md:30).
    /// </para>
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct CanarySystem : ISystem
    {
        /// <summary>
        /// Stage flags shared with the caller: a stage flag is set when that stage ran Burst compiled.
        /// Reset and read directly by the test.
        /// </summary>
        public static readonly SharedStatic<CanaryStages> StageResults =
            SharedStatic<CanaryStages>.GetOrCreate<CanarySystem>();

        /// <inheritdoc/>
        [BurstCompile]
        public void OnCreate(ref SystemState state) => Record(CanaryStages.OnCreate);

        /// <inheritdoc/>
        [BurstCompile]
        public void OnUpdate(ref SystemState state) => Record(CanaryStages.OnUpdate);

        /// <inheritdoc/>
        [BurstCompile]
        public void OnDestroy(ref SystemState state) => Record(CanaryStages.OnDestroy);

        private static void Record(CanaryStages stage)
        {
            var results = StageResults.Data;
            StageResults.Data = BurstProbe.IsBurst() ? results | stage : results & ~stage;
        }
    }

    /// <summary>
    /// Managed twin of <see cref="CanarySystem"/>: identical stages and the same probe, but without
    /// <c>[BurstCompile]</c>, so Entities does not Burst compile its wrappers and no stage may mark
    /// its flag. Together with <see cref="CanarySystem"/> this proves the probe reflects the actual
    /// compilation instead of returning a constant: a probe stuck on <c>true</c> fails here, a probe
    /// stuck on <c>false</c> fails there.
    /// </summary>
    [DisableAutoCreation]
    public partial struct ManagedCanarySystem : ISystem
    {
        /// <summary>Stage flags shared with the caller; expected to stay empty here.</summary>
        public static readonly SharedStatic<CanaryStages> StageResults =
            SharedStatic<CanaryStages>.GetOrCreate<ManagedCanarySystem>();

        /// <inheritdoc/>
        public void OnCreate(ref SystemState state) => Record(CanaryStages.OnCreate);

        /// <inheritdoc/>
        public void OnUpdate(ref SystemState state) => Record(CanaryStages.OnUpdate);

        /// <inheritdoc/>
        public void OnDestroy(ref SystemState state) => Record(CanaryStages.OnDestroy);

        private static void Record(CanaryStages stage)
        {
            var results = StageResults.Data;
            StageResults.Data = BurstProbe.IsBurst() ? results | stage : results & ~stage;
        }
    }

    /// <summary>
    /// Runs <see cref="CanarySystem"/> through its stages and asserts that all of them were Burst
    /// compiled.
    /// </summary>
    [TestFixture]
    public sealed class BurstCanaryTests : RequestTestBase
    {
        private bool _previousSynchronousCompilation;

        /// <summary>
        /// JIT compilation is asynchronous, so without this the first update might still run managed
        /// and the probe would report a false negative. Clearing the flags here, before the harness
        /// creates the world, also runs the system's static constructor and registers the shared
        /// buffer before Burst touches it.
        /// </summary>
        [OneTimeSetUp]
        public void PrepareBurstCanary()
        {
            _previousSynchronousCompilation = BurstCompiler.Options.EnableBurstCompileSynchronously;
            BurstCompiler.Options.EnableBurstCompileSynchronously = true;
            CanarySystem.StageResults.Data = CanaryStages.None;
        }

        /// <summary>Restores the Burst option and clears the shared results.</summary>
        [OneTimeTearDown]
        public void RestoreBurstOptions()
        {
            BurstCompiler.Options.EnableBurstCompileSynchronously = _previousSynchronousCompilation;
            CanarySystem.StageResults.Data = CanaryStages.None;
        }

        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(CanarySystem));
        }

        [Test]
        public void AllStages_RunBurstCompiled()
        {
            Assume.That(BurstCompiler.IsEnabled, "Burst compilation is disabled, nothing to verify");

            UpdateWorld(1);
            DestroyUnmanagedTestSystem<CanarySystem>();

            var stages = CanarySystem.StageResults.Data;
            Assert.IsTrue(stages.HasFlag(CanaryStages.OnCreate), "OnCreate must run Burst compiled");
            Assert.IsTrue(stages.HasFlag(CanaryStages.OnUpdate), "OnUpdate must run Burst compiled");
            Assert.IsTrue(stages.HasFlag(CanaryStages.OnDestroy), "OnDestroy must run Burst compiled");
        }
    }

    /// <summary>
    /// Negative control: runs <see cref="ManagedCanarySystem"/>, the same system without
    /// <c>[BurstCompile]</c>, and asserts that no stage was Burst compiled. Needs no Burst option
    /// handling and is meaningful with Burst on or off.
    /// </summary>
    [TestFixture]
    public sealed class ManagedCanaryTests : RequestTestBase
    {
        /// <summary>Clears the flags and registers the shared buffer before the harness creates the world.</summary>
        [OneTimeSetUp]
        public void PrepareCanary() => ManagedCanarySystem.StageResults.Data = CanaryStages.None;

        /// <summary>Clears the shared results.</summary>
        [OneTimeTearDown]
        public void ClearCanary() => ManagedCanarySystem.StageResults.Data = CanaryStages.None;

        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(ManagedCanarySystem));
        }

        [Test]
        public void AllStages_RunManaged()
        {
            UpdateWorld(1);
            DestroyUnmanagedTestSystem<ManagedCanarySystem>();

            var stages = ManagedCanarySystem.StageResults.Data;
            Assert.IsFalse(stages.HasFlag(CanaryStages.OnCreate), "OnCreate must run managed without [BurstCompile]");
            Assert.IsFalse(stages.HasFlag(CanaryStages.OnUpdate), "OnUpdate must run managed without [BurstCompile]");
            Assert.IsFalse(stages.HasFlag(CanaryStages.OnDestroy), "OnDestroy must run managed without [BurstCompile]");
        }
    }
}
