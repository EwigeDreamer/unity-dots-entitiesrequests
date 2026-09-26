using NUnit.Framework;
using Unity.Collections;

namespace ED.DOTS.EntitiesRequests.Tests
{
    /// <summary>
    /// Request type of the core fixture. The bank and its cards are driven directly, so no ECS
    /// registration and no world are involved.
    /// </summary>
    public struct CoreTestRequest
    {
        /// <summary>Payload value.</summary>
        public int Value;
    }

    /// <summary>
    /// Core semantics of the new bank and card model, ported from the old <c>CoreTests</c>:
    /// write, merge, read, clear, and disposal.
    /// </summary>
    [TestFixture]
    public sealed class CoreTests
    {
        [Test]
        public void CreateAndDispose_Works()
        {
            var bank = new RequestBank<CoreTestRequest>(Allocator.Persistent, 16);
            Assert.IsTrue(bank.IsCreated);

            bank.Dispose();

            Assert.IsFalse(bank.IsCreated);
        }

        [Test]
        public void WriteAndRead_SameFrame_ReadsNothing()
        {
            var bank = new RequestBank<CoreTestRequest>(Allocator.Persistent, 16);
            var writer = new RequestWriter<CoreTestRequest>(bank, 64);
            var reader = new RequestReader<CoreTestRequest>(bank);

            writer.Write(new CoreTestRequest { Value = 42 });

            Assert.That(reader.Read().Length, Is.EqualTo(0));

            reader.Dispose();
            writer.Dispose();
            bank.Dispose();
        }

        [Test]
        public void WriteThenMerge_ThenRead_ReturnsRequests()
        {
            var bank = new RequestBank<CoreTestRequest>(Allocator.Persistent, 16);
            var writer = new RequestWriter<CoreTestRequest>(bank, 64);
            var reader = new RequestReader<CoreTestRequest>(bank);

            writer.Write(new CoreTestRequest { Value = 1 });
            writer.Write(new CoreTestRequest { Value = 2 });

            bank.Merge();

            var requests = reader.Read();
            Assert.That(requests.Length, Is.EqualTo(2));
            Assert.That(requests[0].Value, Is.EqualTo(1));
            Assert.That(requests[1].Value, Is.EqualTo(2));

            reader.Dispose();
            writer.Dispose();
            bank.Dispose();
        }

        [Test]
        public void MultipleWrites_ReadAll_InOrder()
        {
            var bank = new RequestBank<CoreTestRequest>(Allocator.Persistent, 16);
            var writer = new RequestWriter<CoreTestRequest>(bank, 100);
            var reader = new RequestReader<CoreTestRequest>(bank);

            for (var i = 0; i < 100; i++)
            {
                writer.Write(new CoreTestRequest { Value = i });
            }

            bank.Merge();

            var requests = reader.Read();
            Assert.That(requests.Length, Is.EqualTo(100));
            for (var i = 0; i < requests.Length; i++)
            {
                Assert.That(requests[i].Value, Is.EqualTo(i));
            }

            reader.Dispose();
            writer.Dispose();
            bank.Dispose();
        }

        [Test]
        public void Merge_ClearsWriteBuffer()
        {
            var bank = new RequestBank<CoreTestRequest>(Allocator.Persistent, 16);
            var writer = new RequestWriter<CoreTestRequest>(bank, 64);
            var reader = new RequestReader<CoreTestRequest>(bank);

            Assert.That(reader.Read().Length, Is.EqualTo(0));

            writer.Write(new CoreTestRequest { Value = 123 });
            Assert.That(reader.Read().Length, Is.EqualTo(0));

            bank.Merge();
            Assert.That(reader.Read().Length, Is.EqualTo(1));

            writer.Write(new CoreTestRequest { Value = 456 });
            Assert.That(reader.Read().Length, Is.EqualTo(1));

            bank.Merge();

            var requests = reader.Read();
            Assert.That(requests.Length, Is.EqualTo(2));
            Assert.That(requests[0].Value, Is.EqualTo(123));
            Assert.That(requests[1].Value, Is.EqualTo(456));

            reader.Clear();
            Assert.That(reader.Read().Length, Is.EqualTo(0));

            reader.Dispose();
            writer.Dispose();
            bank.Dispose();
        }

        [Test]
        public void CachedWriterAndReader_WorkAcrossMerges()
        {
            var bank = new RequestBank<CoreTestRequest>(Allocator.Persistent, 16);
            var writer = new RequestWriter<CoreTestRequest>(bank, 64);
            var reader = new RequestReader<CoreTestRequest>(bank);

            writer.Write(new CoreTestRequest { Value = 100 });
            bank.Merge();
            Assert.That(reader.Read().Length, Is.EqualTo(1));

            writer.Write(new CoreTestRequest { Value = 200 });
            bank.Merge();

            var requests = reader.Read();
            Assert.That(requests.Length, Is.EqualTo(2));
            Assert.That(requests[0].Value, Is.EqualTo(100));
            Assert.That(requests[1].Value, Is.EqualTo(200));

            reader.Clear();
            Assert.That(reader.Read().Length, Is.EqualTo(0));

            reader.Dispose();
            writer.Dispose();
            bank.Dispose();
        }
    }
}
