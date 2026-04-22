using System.Linq;
using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;

namespace ED.DOTS.EntitiesRequests.Tests
{
    /// <summary>
    /// Base class for ECS PlayMode tests that require an isolated world.
    /// Provides automatic setup and cleanup of a fresh World for each test.
    /// </summary>
    public abstract class ECSTestBase
    {
        protected World World { get; private set; }
        protected EntityManager EntityManager => World.EntityManager;

        private World _previousWorld;

        [SetUp]
        public virtual void SetUp()
        {
            // Store the previous default world to restore later
            _previousWorld = World.DefaultGameObjectInjectionWorld;

            // Create a fresh world for this test
            World = new World("Test World", WorldFlags.Game);

            // Set it as the default injection world so that queries and singletons work
            World.DefaultGameObjectInjectionWorld = World;

            // Ensure systems do not automatically update unless explicitly called
            var initializationGroup = World.GetOrCreateSystemManaged<InitializationSystemGroup>();
            var simulationGroup = World.GetOrCreateSystemManaged<SimulationSystemGroup>();
            var presentationGroup = World.GetOrCreateSystemManaged<PresentationSystemGroup>();
            
            var requestSystemGroup = World.GetOrCreateSystemManaged<RequestSystemGroup>();
            simulationGroup.AddSystemToUpdateList(requestSystemGroup);
            simulationGroup.SortSystems();

            RegisterRequestSystems(World);
        }

        [TearDown]
        public virtual void TearDown()
        {
            // Restore the previous default world
            World.DefaultGameObjectInjectionWorld = _previousWorld;

            // Dispose the test world and all its resources
            if (World.IsCreated)
                World.Dispose();

            World = null;
        }
        
        protected abstract void RegisterRequestSystems(World world);

        /// <summary>
        /// Helper to complete all scheduled jobs and update the world for a number of frames.
        /// </summary>
        protected void UpdateWorld(int frameCount = 1)
        {
            for (int i = 0; i < frameCount; i++)
            {
                World.Update();
                EntityManager.CompleteAllTrackedJobs();
            }
        }

        /// <summary>
        /// Ensures all tracked jobs are completed.
        /// </summary>
        protected void CompleteJobs() => EntityManager.CompleteAllTrackedJobs();

        protected T GetOrAddRequestSystem<T>() where T : SystemBase
        {
            foreach (var system in World.Systems)
                if (system is T result)
                    return result;
            var newSystem = World.GetOrCreateSystemManaged<T>();
            World.GetOrCreateSystemManaged<RequestSystemGroup>().AddSystemToUpdateList(newSystem);
            World.GetOrCreateSystemManaged<RequestSystemGroup>().SortSystems();
            return newSystem;
        }

        protected T GetOrAddSystemToSimulationManaged<T>() where T : SystemBase
        {
            foreach (var system in World.Systems)
                if (system is T result)
                    return result;
            var newSystem = World.GetOrCreateSystemManaged<T>();
            World.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(newSystem);
            World.GetOrCreateSystemManaged<SimulationSystemGroup>().SortSystems();
            return newSystem;
        }

        protected ref T GetOrAddSystemToSimulation<T>() where T : unmanaged, ISystem
        {
            var desiredTypeIndex = TypeManager.GetSystemTypeIndex<T>();
            using var systems = World.Unmanaged.GetAllUnmanagedSystems(Allocator.Temp);
            foreach (var handle in systems)
            {
                var typeIndex = World.Unmanaged.GetSystemTypeIndex(handle);
                if (typeIndex != desiredTypeIndex) continue;
                return ref World.Unmanaged.GetUnsafeSystemRef<T>(handle);
            }
            var newHandle = World.GetOrCreateSystem<T>();
            World.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(newHandle);
            World.GetOrCreateSystemManaged<SimulationSystemGroup>().SortSystems();
            return ref World.Unmanaged.GetUnsafeSystemRef<T>(newHandle);
        }
    }
}