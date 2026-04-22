using NUnit.Framework;
using Unity.Entities;
using ED.DOTS.EntitiesRequests;

[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tests.IntegrationTestRequest))]

namespace ED.DOTS.EntitiesRequests.Tests
{
    public struct IntegrationTestRequest
    {
        public int Value;
    }

    [TestFixture]
    public class ECSIntegrationTests : ECSTestBase
    {
        protected override void RegisterRequestSystems(World world)
        {
            GetOrAddRequestSystem<IntegrationTestRequest_RequestSystem>();
        }

        // --- Тестовые системы ---

        // ISystem вариант писателя
        [DisableAutoCreation]
        public partial struct WriterISystem : ISystem
        {
            private RequestWriter<IntegrationTestRequest> _writer;

            public void OnCreate(ref SystemState state)
            {
                _writer = state.GetRequestWriter<IntegrationTestRequest>();
            }

            public void OnUpdate(ref SystemState state)
            {
                _writer.Write(new IntegrationTestRequest { Value = 1 });
            }
        }

        // SystemBase вариант писателя
        [DisableAutoCreation]
        public partial class WriterSystemBase : SystemBase
        {
            private RequestWriter<IntegrationTestRequest> _writer;

            protected override void OnCreate()
            {
                _writer = this.GetRequestWriter<IntegrationTestRequest>();
            }

            protected override void OnUpdate()
            {
                _writer.Write(new IntegrationTestRequest { Value = 2 });
            }
        }

        // ISystem читатель
        [DisableAutoCreation]
        public partial struct ReaderISystem : ISystem
        {
            private RequestReader<IntegrationTestRequest> _reader;
            public int ReceivedCount;

            public void OnCreate(ref SystemState state)
            {
                _reader = state.GetRequestReader<IntegrationTestRequest>();
            }

            public void OnUpdate(ref SystemState state)
            {
                ReceivedCount = 0;
                foreach (var req in _reader.Read())
                {
                    ReceivedCount++;
                }
                // После чтения очищаем буфер
                _reader.Clear();
            }
        }

        // SystemBase читатель
        [DisableAutoCreation]
        public partial class ReaderSystemBase : SystemBase
        {
            private RequestReader<IntegrationTestRequest> _reader;
            public int ReceivedCount { get; private set; }

            protected override void OnCreate()
            {
                _reader = this.GetRequestReader<IntegrationTestRequest>();
            }

            protected override void OnUpdate()
            {
                ReceivedCount = 0;
                foreach (var req in _reader.Read())
                {
                    ReceivedCount++;
                }
                _reader.Clear();
            }
        }

        // --- Тесты ---

        [Test]
        public void GetWriter_FromSystemState_CreatesSingletonAndReturnsValidWriter()
        {
            var query = EntityManager.CreateEntityQuery(typeof(RequestSingleton<IntegrationTestRequest>));
            Assert.IsFalse(query.TryGetSingleton<RequestSingleton<IntegrationTestRequest>>(out _));

            var writer = EntityManager.GetRequestWriter<IntegrationTestRequest>();
            writer.Write(new IntegrationTestRequest { Value = 0 });

            Assert.IsTrue(query.TryGetSingleton<RequestSingleton<IntegrationTestRequest>>(out _));
        }

        [Test]
        public void WriterISystem_And_ReaderISystem_ExchangeRequests()
        {
            ref var writer = ref GetOrAddSystemToSimulation<WriterISystem>();
            ref var reader = ref GetOrAddSystemToSimulation<ReaderISystem>();

            // Первый кадр: запись
            UpdateWorld(1);
            // В первом кадре reader ещё не видит запрос, потому что Update ещё не переместил write в read
            Assert.AreEqual(0, reader.ReceivedCount);

            // Второй кадр: writer снова пишет, reader должен увидеть запрос из первого кадра
            UpdateWorld(1);
            Assert.AreEqual(1, reader.ReceivedCount);
        }

        [Test]
        public void WriterSystemBase_And_ReaderSystemBase_ExchangeRequests()
        {
            var writer = GetOrAddSystemToSimulationManaged<WriterSystemBase>();
            var reader = GetOrAddSystemToSimulationManaged<ReaderSystemBase>();

            UpdateWorld(1);
            Assert.AreEqual(0, reader.ReceivedCount);

            UpdateWorld(1);
            Assert.AreEqual(1, reader.ReceivedCount);
        }

        [Test]
        public unsafe void EnsureRequestBufferCapacity_FromSystemState_Works()
        {
            var capacity = 1024;
            EntityManager.EnsureRequestBufferCapacity<IntegrationTestRequest>(capacity);
            var singleton = EntitiesRequestsHelper.GetOrCreateSingleton<IntegrationTestRequest>(EntityManager);
            var writer = singleton.Requests.GetWriter();
            for (int i = 0; i < capacity; i++)
            {
                writer.WriteNoResize(new IntegrationTestRequest { Value = i });
            }
            // Если дошли сюда без исключений — ёмкости хватило.
        }

        [Test]
        public void GetWriter_FromEntityManager_ReturnsSameWriter()
        {
            var writer1 = EntityManager.GetRequestWriter<IntegrationTestRequest>();
            var writer2 = EntityManager.GetRequestWriter<IntegrationTestRequest>();
            writer1.Write(new IntegrationTestRequest { Value = 5 });
            writer2.Write(new IntegrationTestRequest { Value = 6 });

            var reader = EntityManager.GetRequestReader<IntegrationTestRequest>();
            // Пока Update не вызван, reader не должен видеть запросы
            using var enumerator = reader.Read().GetEnumerator();
            Assert.IsFalse(enumerator.MoveNext());

            // Вызовем Update вручную
            var singleton = EntitiesRequestsHelper.GetOrCreateSingleton<IntegrationTestRequest>(EntityManager);
            singleton.Requests.Update();

            var readerAfter = EntityManager.GetRequestReader<IntegrationTestRequest>();
            int count = 0;
            foreach (var req in readerAfter.Read())
                count++;
            Assert.AreEqual(2, count);
            
            // Очищаем, чтобы не влиять на другие тесты
            readerAfter.Clear();
        }
    }
}