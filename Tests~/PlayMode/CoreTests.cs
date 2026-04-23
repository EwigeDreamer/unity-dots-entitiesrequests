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
            var writer = requests.GetWriter();
            writer.Write(new TestRequest { Value = 42 });

            var reader = requests.GetReader();
            using var enumerator = reader.Read().GetEnumerator();
            Assert.IsFalse(enumerator.MoveNext());

            requests.Dispose();
        }

        [Test]
        public void WriteThenUpdate_ThenRead_ReturnsRequests()
        {
            var requests = new Requests<TestRequest>(16, Allocator.Persistent);

            var writer = requests.GetWriter();
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

            requests.Dispose();
        }

        [Test]
        public void MultipleWrites_ReadAll_InOrder()
        {
            var requests = new Requests<TestRequest>(16, Allocator.Persistent);

            var writer = requests.GetWriter();
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

            requests.Dispose();
        }

        [Test]
        public void Update_ClearsWriteBuffer()
        {
            var requests = new Requests<TestRequest>(16, Allocator.Persistent);
            var reader = requests.GetReader();

            // Initially read buffer empty
            using (var enumerator = reader.Read().GetEnumerator())
                Assert.IsFalse(enumerator.MoveNext());

            var writer = requests.GetWriter();
            writer.Write(new TestRequest { Value = 123 });

            // Still empty because Update not called
            using (var enumerator = reader.Read().GetEnumerator())
                Assert.IsFalse(enumerator.MoveNext());

            requests.Update();

            // Now read buffer contains the request
            using (var enumerator = reader.Read().GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(123, enumerator.Current.Value);
                Assert.IsFalse(enumerator.MoveNext());
            }

            // Write another request after Update
            writer.Write(new TestRequest { Value = 456 });

            // Read buffer still has old request (not cleared yet)
            using (var enumerator = reader.Read().GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(123, enumerator.Current.Value);
                Assert.IsFalse(enumerator.MoveNext());
            }

            requests.Update();

            // Actually correct behavior: read buffer accumulates all requests from all previous Updates until Clear is called.
            // Let's adjust test to reflect that: After second Update, read buffer should have 123 and 456.
            // Then after Clear, it should be empty.
            using (var enumerator = reader.Read().GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(123, enumerator.Current.Value);
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(456, enumerator.Current.Value);
                Assert.IsFalse(enumerator.MoveNext());
            }

            // Clear the read buffer
            reader.Clear();

            using (var enumerator = reader.Read().GetEnumerator())
                Assert.IsFalse(enumerator.MoveNext());

            requests.Dispose();
        }

        [Test]
        public void CachedWriterAndReader_WorkAcrossUpdates()
        {
            var requests = new Requests<TestRequest>(16, Allocator.Persistent);

            // Cache writer and reader
            var writer = requests.GetWriter();
            var reader = requests.GetReader();

            // First frame: write
            writer.Write(new TestRequest { Value = 100 });
            requests.Update();

            // Read should return the request
            using (var enumerator = reader.Read().GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(100, enumerator.Current.Value);
                Assert.IsFalse(enumerator.MoveNext());
            }

            // Second frame: write again
            writer.Write(new TestRequest { Value = 200 });
            requests.Update();

            // Now read buffer contains both (since we haven't cleared)
            using (var enumerator = reader.Read().GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(100, enumerator.Current.Value);
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreEqual(200, enumerator.Current.Value);
                Assert.IsFalse(enumerator.MoveNext());
            }

            // Clear and verify
            reader.Clear();
            using (var enumerator = reader.Read().GetEnumerator())
                Assert.IsFalse(enumerator.MoveNext());

            requests.Dispose();
        }
    }
}