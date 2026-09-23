using NUnit.Framework;
using Unity.Collections;
using ED.DOTS.EntitiesRequests;
using Unity.Entities;

namespace ED.DOTS.EntitiesRequests.Tests
{
    [TestFixture]
    public class CoreTests : ECSTestBase
    {
        protected override void RegisterRequestSystems(World world) { }

        private struct TestRequest
        {
            public int Value;
        }

        [Test]
        public void CreateAndDispose_Works()
        {
            var requests = new Requests<TestRequest>(16, Allocator.Persistent);
            Assert.IsTrue(requests.IsCreated);
            requests.Dispose();
            Assert.IsFalse(requests.IsCreated);
        }

        [Test]
        public void WriteAndRead_SameFrame_ReadsNothing()
        {
            var requests = new Requests<TestRequest>(16, Allocator.Persistent);
            var writer = requests.GetWriter(64);
            writer.Write(new TestRequest { Value = 42 });

            var reader = requests.GetReader();
            using var enumerator = reader.Read().GetEnumerator();
            Assert.IsFalse(enumerator.MoveNext());

            writer.Dispose();
            requests.Dispose();
        }

        [Test]
        public void WriteThenUpdate_ThenRead_ReturnsRequests()
        {
            var requests = new Requests<TestRequest>(16, Allocator.Persistent);
            var writer = requests.GetWriter(64);
            writer.Write(new TestRequest { Value = 1 });
            writer.Write(new TestRequest { Value = 2 });

            requests.Update();

            var reader = requests.GetReader();
            using var enumerator = reader.Read().GetEnumerator();

            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual(1, enumerator.Current.Value);
            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual(2, enumerator.Current.Value);
            Assert.IsFalse(enumerator.MoveNext());

            writer.Dispose();
            requests.Dispose();
        }

        [Test]
        public void MultipleWrites_ReadAll_InOrder()
        {
            var requests = new Requests<TestRequest>(16, Allocator.Persistent);
            var writer = requests.GetWriter(100);
            for (int i = 0; i < 100; i++)
            {
                writer.Write(new TestRequest { Value = i });
            }

            requests.Update();

            var reader = requests.GetReader();
            using var enumerator = reader.Read().GetEnumerator();

            int expected = 0;
            while (enumerator.MoveNext())
            {
                Assert.AreEqual(expected, enumerator.Current.Value);
                expected++;
            }
            Assert.AreEqual(100, expected);

            writer.Dispose();
            requests.Dispose();
        }

        [Test]
        public void Update_ClearsWriteBuffer()
        {
            var requests = new Requests<TestRequest>(16, Allocator.Persistent);
            var reader = requests.GetReader();

            using (var enumerator = reader.Read().GetEnumerator())
                Assert.IsFalse(enumerator.MoveNext());

            var writer = requests.GetWriter(64);
            writer.Write(new TestRequest { Value = 123 });

            using (var enumerator = reader.Read().GetEnumerator())
                Assert.IsFalse(enumerator.MoveNext());

            requests.Update();

            using (var enumerator = reader.Read().GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(123, enumerator.Current.Value);
                Assert.IsFalse(enumerator.MoveNext());
            }

            writer.Write(new TestRequest { Value = 456 });

            using (var enumerator = reader.Read().GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(123, enumerator.Current.Value);
                Assert.IsFalse(enumerator.MoveNext());
            }

            requests.Update();

            using (var enumerator = reader.Read().GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(123, enumerator.Current.Value);
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(456, enumerator.Current.Value);
                Assert.IsFalse(enumerator.MoveNext());
            }

            reader.Clear();

            using (var enumerator = reader.Read().GetEnumerator())
                Assert.IsFalse(enumerator.MoveNext());

            writer.Dispose();
            requests.Dispose();
        }

        [Test]
        public void CachedWriterAndReader_WorkAcrossUpdates()
        {
            var requests = new Requests<TestRequest>(16, Allocator.Persistent);
            var writer = requests.GetWriter(64);
            var reader = requests.GetReader();

            writer.Write(new TestRequest { Value = 100 });
            requests.Update();

            using (var enumerator = reader.Read().GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(100, enumerator.Current.Value);
                Assert.IsFalse(enumerator.MoveNext());
            }

            writer.Write(new TestRequest { Value = 200 });
            requests.Update();

            using (var enumerator = reader.Read().GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(100, enumerator.Current.Value);
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(200, enumerator.Current.Value);
                Assert.IsFalse(enumerator.MoveNext());
            }

            reader.Clear();
            using (var enumerator = reader.Read().GetEnumerator())
                Assert.IsFalse(enumerator.MoveNext());

            writer.Dispose();
            requests.Dispose();
        }
    }
}