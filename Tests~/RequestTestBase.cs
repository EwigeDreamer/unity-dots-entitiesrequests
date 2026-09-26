using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Tests
{
    /// <summary>
    /// Base harness for ECS-level tests of the requests core. It builds an isolated world and lets
    /// Unity place every system by its group and order attributes, so fixtures never wire groups or
    /// sort systems by hand. The world is not registered as the default injection world and is not
    /// appended to the player loop: it is driven explicitly and therefore stays deterministic.
    /// </summary>
    public abstract class RequestTestBase
    {
        /// <summary>World created for the current test.</summary>
        protected World World { get; private set; }

        /// <summary>Entity manager of <see cref="World"/>.</summary>
        protected EntityManager EntityManager => World.EntityManager;

        /// <summary>Creates the test world and fills it with the fixture's systems.</summary>
        [SetUp]
        public virtual void SetUp()
        {
            World = new World("Requests Test World", WorldFlags.Game);

            var systems = new List<Type>
            {
                // Infrastructure every owner needs: the group it is placed into and the ordering
                // anchor its [UpdateAfter] points at. The three root groups are not listed here,
                // because AddSystemsToRootLevelSystemGroups creates them itself.
                typeof(RequestSystemGroup),
                typeof(LateSimulationSystemGroup),
            };
            CollectSystems(systems);

            // Creates the root groups, places every system by its [UpdateInGroup]/[UpdateBefore]/
            // [UpdateAfter] and sorts them. No manual AddSystemToUpdateList/SortSystems.
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World, systems);
        }

        /// <summary>Disposes the test world.</summary>
        [TearDown]
        public virtual void TearDown()
        {
            if (World is { IsCreated: true })
            {
                World.Dispose();
            }

            World = null;
        }

        /// <summary>
        /// Adds every system of this fixture: the generated request owners and the test writers and
        /// readers. Request type names must be unique across the whole test assembly, because a
        /// name collision produces duplicate generated systems and fails the build.
        /// <para>
        /// Add the generated owner <b>last</b>: the world destroys systems in reverse creation order,
        /// so the owner then dies first on teardown with live cards still around — the order that used
        /// to crash the client build.
        /// </para>
        /// </summary>
        /// <param name="systems">List of system types to append this fixture's systems to.</param>
        protected virtual void CollectSystems(List<Type> systems) { }

        /// <summary>Runs the world for the given number of frames, completing tracked jobs after each.</summary>
        /// <param name="frames">Number of frames to update for.</param>
        protected void UpdateWorld(int frames = 1)
        {
            for (var i = 0; i < frames; i++)
            {
                World.Update();
                EntityManager.CompleteAllTrackedJobs();
            }
        }

        /// <summary>Completes every job tracked by the world.</summary>
        protected void CompleteJobs() => EntityManager.CompleteAllTrackedJobs();

        /// <summary>Returns a reference to a system created for this fixture.</summary>
        /// <typeparam name="T">Unmanaged system type added by <see cref="CollectSystems"/>.</typeparam>
        protected ref T GetTestSystem<T>() where T : unmanaged, ISystem
        {
            var handle = World.GetOrCreateSystem<T>();
            return ref World.Unmanaged.GetUnsafeSystemRef<T>(handle);
        }

        /// <summary>
        /// Destroys a managed system created for this fixture, removing it from every group that holds
        /// it first. Used by tests where a writer or reader has to disappear mid-run.
        /// <para>
        /// Overloading this name for managed and unmanaged systems is impossible: C# does not tell
        /// generic methods apart by their constraints, so the two flavours carry distinct names.
        /// </para>
        /// </summary>
        /// <typeparam name="T">Managed system type added by <see cref="CollectSystems"/>.</typeparam>
        protected void DestroyManagedTestSystem<T>() where T : ComponentSystemBase
        {
            var target = World.GetExistingSystemManaged<T>();
            if (target == null)
            {
                return;
            }

            foreach (var system in World.Systems)
            {
                if (!(system is ComponentSystemGroup group))
                {
                    continue;
                }

                if (!ContainsManaged(group, target))
                {
                    continue;
                }

                group.RemoveSystemFromUpdateList(target);
                group.SortSystems();
            }

            World.DestroySystemManaged(target);
        }

        /// <summary>
        /// Destroys an unmanaged system created for this fixture, removing it from every group that
        /// holds it first: <see cref="World.DestroySystem"/> does not detach systems itself, and a
        /// system may be listed in more than one group.
        /// </summary>
        /// <typeparam name="T">Unmanaged system type added by <see cref="CollectSystems"/>.</typeparam>
        protected void DestroyUnmanagedTestSystem<T>() where T : unmanaged, ISystem
        {
            var target = World.GetExistingSystem<T>();
            if (target == SystemHandle.Null)
            {
                return;
            }

            foreach (var system in World.Systems)
            {
                if (!(system is ComponentSystemGroup group))
                {
                    continue;
                }

                if (!ContainsUnmanaged(group, target))
                {
                    continue;
                }

                group.RemoveSystemFromUpdateList(target);
                group.SortSystems();
            }

            World.DestroySystem(target);
        }

        private static bool ContainsManaged(ComponentSystemGroup group, ComponentSystemBase system)
        {
            var managed = group.ManagedSystems;
            for (var i = 0; i < managed.Count; i++)
            {
                if (ReferenceEquals(managed[i], system))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsUnmanaged(ComponentSystemGroup group, SystemHandle system)
        {
            using var handles = group.GetUnmanagedSystems(Allocator.Temp);
            for (var i = 0; i < handles.Length; i++)
            {
                if (handles[i] == system)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to read the bank published by the request singleton of <typeparamref name="TRequest"/>.
        /// </summary>
        /// <typeparam name="TRequest">Unmanaged request type.</typeparam>
        /// <param name="bank">The bank when the singleton entity exists, otherwise default.</param>
        /// <returns>True when the bank singleton exists.</returns>
        protected bool TryGetBank<TRequest>(out RequestBank<TRequest> bank) where TRequest : unmanaged
        {
            var query = EntityManager.CreateEntityQuery(typeof(RequestSingleton<TRequest>));
            if (query.TryGetSingleton<RequestSingleton<TRequest>>(out var singleton))
            {
                bank = singleton.Bank;
                return true;
            }

            bank = default;
            return false;
        }
    }
}
