using System;
using System.Collections.Generic;
using ED.DOTS.EntitiesRequests.Tmp;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

[assembly: RegisterRequest(typeof(ED.DOTS.EntitiesRequests.Tmp.Tests.MultiSystemRequest))]

namespace ED.DOTS.EntitiesRequests.Tmp.Tests
{
    /// <summary>Request type of the multi system fixture.</summary>
    public struct MultiSystemRequest
    {
        /// <summary>Payload value.</summary>
        public int Value;

        /// <summary>Writer that produced the request.</summary>
        public int WriterId;
    }

    /// <summary>
    /// Several independent writer systems feeding the same bank, with per-test request counts.
    /// Ported from <c>MultiSystemMultiWriterTests</c>. Parameters are configured after setup,
    /// before the first update, because the harness creates systems in batch. Unused writers default
    /// to zero requests, so they stay inert in tests that do not configure them.
    /// </summary>
    [TestFixture]
    public sealed class NewMultiSystemTests : RequestTestBase
    {
        /// <inheritdoc/>
        protected override void CollectSystems(List<Type> systems)
        {
            systems.Add(typeof(MultiSystemRequest_RequestSystem));
            systems.Add(typeof(WriterSystem1));
            systems.Add(typeof(WriterSystem2));
            systems.Add(typeof(WriterSystem3));
            systems.Add(typeof(ReaderSystem));
        }

        [Test]
        public void MultipleWriters_IndependentBufferExpansion()
        {
            var writer1 = World.GetExistingSystemManaged<WriterSystem1>();
            writer1.WriterId = 1;
            writer1.RequestCount = 500;

            var writer2 = World.GetExistingSystemManaged<WriterSystem2>();
            writer2.WriterId = 2;
            writer2.RequestCount = 200;

            UpdateWorld(2);

            var reader = World.GetExistingSystemManaged<ReaderSystem>();
            Assert.That(reader.ReceivedCount, Is.EqualTo(writer1.RequestCount + writer2.RequestCount));
            for (var i = 0; i < writer1.RequestCount; i++)
            {
                Assert.IsTrue(reader.ReceivedValues.Contains(1 * 10000 + i));
            }

            for (var i = 0; i < writer2.RequestCount; i++)
            {
                Assert.IsTrue(reader.ReceivedValues.Contains(2 * 10000 + i));
            }
        }

        [Test]
        public void ReadBufferExpansion_WithManyWriters()
        {
            const int count = 200;

            var writer1 = World.GetExistingSystemManaged<WriterSystem1>();
            writer1.WriterId = 1;
            writer1.RequestCount = count;

            var writer2 = World.GetExistingSystemManaged<WriterSystem2>();
            writer2.WriterId = 2;
            writer2.RequestCount = count;

            var writer3 = World.GetExistingSystemManaged<WriterSystem3>();
            writer3.WriterId = 3;
            writer3.RequestCount = count;

            UpdateWorld(2);

            var reader = World.GetExistingSystemManaged<ReaderSystem>();
            Assert.That(reader.ReceivedCount, Is.EqualTo(count * 3));
            for (var w = 1; w <= 3; w++)
            {
                for (var v = 0; v < count; v++)
                {
                    Assert.IsTrue(reader.ReceivedValues.Contains(w * 10000 + v));
                }
            }
        }

        [DisableAutoCreation]
        public partial class WriterSystem1 : SystemBase
        {
            public int WriterId;
            public int RequestCount;

            private RequestWriter<MultiSystemRequest> _writer;

            protected override void OnCreate() => _writer = this.GetRequestWriter<MultiSystemRequest>();
            protected override void OnDestroy() => _writer.Dispose();

            protected override void OnUpdate()
            {
                for (var i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new MultiSystemRequest { Value = i, WriterId = WriterId });
                }
            }
        }

        [DisableAutoCreation]
        public partial class WriterSystem2 : SystemBase
        {
            public int WriterId;
            public int RequestCount;

            private RequestWriter<MultiSystemRequest> _writer;

            protected override void OnCreate() => _writer = this.GetRequestWriter<MultiSystemRequest>();
            protected override void OnDestroy() => _writer.Dispose();

            protected override void OnUpdate()
            {
                for (var i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new MultiSystemRequest { Value = i, WriterId = WriterId });
                }
            }
        }

        [DisableAutoCreation]
        public partial class WriterSystem3 : SystemBase
        {
            public int WriterId;
            public int RequestCount;

            private RequestWriter<MultiSystemRequest> _writer;

            protected override void OnCreate() => _writer = this.GetRequestWriter<MultiSystemRequest>();
            protected override void OnDestroy() => _writer.Dispose();

            protected override void OnUpdate()
            {
                for (var i = 0; i < RequestCount; i++)
                {
                    _writer.Write(new MultiSystemRequest { Value = i, WriterId = WriterId });
                }
            }
        }

        [DisableAutoCreation]
        public partial class ReaderSystem : SystemBase
        {
            private RequestReader<MultiSystemRequest> _reader;

            public NativeHashSet<int> ReceivedValues;
            public int ReceivedCount;

            protected override void OnCreate()
            {
                _reader = this.GetRequestReader<MultiSystemRequest>();
                ReceivedValues = new NativeHashSet<int>(10000, Allocator.Persistent);
            }

            protected override void OnDestroy()
            {
                ReceivedValues.Dispose();
                _reader.Dispose();
            }

            protected override void OnUpdate()
            {
                ReceivedValues.Clear();
                ReceivedCount = 0;
                foreach (var request in _reader.Read())
                {
                    ReceivedCount++;
                    ReceivedValues.Add(request.WriterId * 10000 + request.Value);
                }
                _reader.Clear();
            }
        }
    }
}
