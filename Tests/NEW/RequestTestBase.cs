using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Tmp.Tests
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
