using System;
using ED.DOTS.EntitiesRequests.Tests;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace ED.DOTS.EntitiesRequests.Tests
{
    /// <summary>Request type of the bank lifetime fixture.</summary>
    public struct BankLifetimeRequest
    {
        /// <summary>Payload value.</summary>
        public int Value;
    }

    /// <summary>
    /// Lifetime contract of the new bank and card model, verified with the tracking allocator so that
    /// use-after-free and double-free are caught deterministically:
    /// <list type="bullet">
    /// <item>after the bank is disposed, live cards become invalid and their blocks survive the bank;</item>
    /// <item>operations on an invalid card or a dead bank are logged no-ops, never memory access;</item>
    /// <item>disposal is idempotent and every block is freed exactly once, in any order.</item>
    /// </list>
    /// </summary>
    [TestFixture]
    public sealed class BankLifetimeTests
    {
        private AllocatorHelper<TrackingAllocator> _helper;

        private ref TrackingAllocator Alloc => ref _helper.Allocator;

        [SetUp]
        public void SetUp()
        {
            _helper = new AllocatorHelper<TrackingAllocator>(AllocatorManager.Persistent);
            _helper.Allocator.Initialize(AllocatorManager.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            _helper.Allocator.Dispose();
            _helper.Dispose();
        }

        [Test]
        public unsafe void BankDisposed_WhileCardsAlive_CardsBecomeInvalid()
        {
            var bank = new RequestBank<BankLifetimeRequest>(Alloc.Handle, 16);
            var writer = new RequestWriter<BankLifetimeRequest>(bank, Alloc.Handle, 16);
            var reader = new RequestReader<BankLifetimeRequest>(bank, Alloc.Handle);
            var bankBlock = (IntPtr)bank.Data;

            bank.Dispose();

            Assert.That(Alloc.IsLive(bankBlock), Is.False, "the bank block must be freed");
            Assert.That(Alloc.LiveCount, Is.GreaterThan(0), "card blocks must outlive the bank");
            Assert.IsFalse(writer.IsValid);
            Assert.IsFalse(reader.IsValid);

            LogAssert.Expect(LogType.Error, "[Requests] RequestWriter.Write ignored: the card is not valid.");
            writer.Write(new BankLifetimeRequest { Value = 1 });

            LogAssert.Expect(LogType.Error, "[Requests] ParallelWriter.WriteNoResize ignored: the card is not valid.");
            writer.AsParallelWriter().WriteNoResize(new BankLifetimeRequest { Value = 2 });

            LogAssert.Expect(LogType.Error, "[Requests] RequestReader.Read ignored: the card is not valid.");
            Assert.That(reader.Read().Length, Is.EqualTo(0));

            LogAssert.Expect(LogType.Error, "[Requests] RequestReader.Clear ignored: the card is not valid.");
            reader.Clear();

            writer.Dispose();
            reader.Dispose();

            Assert.That(Alloc.DoubleFreeCount, Is.EqualTo(0));
            Assert.That(Alloc.LiveCount, Is.EqualTo(0), "every block must be freed after the cards are disposed");
        }

        [Test]
        public void CardsDisposedFirst_ThenBank_NoDoubleFree()
        {
            var bank = new RequestBank<BankLifetimeRequest>(Alloc.Handle, 16);
            var writer = new RequestWriter<BankLifetimeRequest>(bank, Alloc.Handle, 16);
            var reader = new RequestReader<BankLifetimeRequest>(bank, Alloc.Handle);

            writer.Dispose();
            reader.Dispose();
            bank.Dispose();

            Assert.That(Alloc.DoubleFreeCount, Is.EqualTo(0), "a writer buffer must not be freed twice");
            Assert.That(Alloc.LiveCount, Is.EqualTo(0));
        }

        [Test]
        public void DoubleBankDispose_IsIdempotent()
        {
            var bank = new RequestBank<BankLifetimeRequest>(Alloc.Handle, 16);

            bank.Dispose();
            bank.Dispose();

            Assert.That(Alloc.DoubleFreeCount, Is.EqualTo(0));
            Assert.That(Alloc.LiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Merge_AfterBankDeath_IsLoggedNoOp()
        {
            var bank = new RequestBank<BankLifetimeRequest>(Alloc.Handle, 16);
            bank.Dispose();

            LogAssert.Expect(LogType.Error, "[Requests] RequestBank.Merge ignored: the bank is not created.");
            bank.Merge();

            Assert.That(Alloc.DoubleFreeCount, Is.EqualTo(0));
            Assert.That(Alloc.LiveCount, Is.EqualTo(0));
        }
    }
}
