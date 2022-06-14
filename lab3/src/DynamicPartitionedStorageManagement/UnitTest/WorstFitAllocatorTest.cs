﻿////////////////////////////////////////////////////////////////////////////////
//
// This file is part of the THUEE_OS project.
//
// Copyright (C) 2022 Timothy-LiuXuefeng
//
// MIT License
//

namespace UnitTest
{
    [TestClass]
    public class WorstFitAllocatorTest
    {
        [TestMethod]
        public void TestAlloction()
        {
            var manager = MemoryManagerFactory.CreateMemoryManager(0u, 1024, MemoryManagerFactory.AllocationStrategy.WorstFit);
            Assert.IsNotNull(manager);
            {
                var memory = manager.AllocateMemory(16);
                Assert.IsNotNull(memory);
                Assert.IsTrue(memory.Value >= 0 && memory.Value < 1024);
            }
        }

        [TestMethod]
        public void TestFree()
        {
            var manager = MemoryManagerFactory.CreateMemoryManager(0u, 1024, MemoryManagerFactory.AllocationStrategy.WorstFit);
            {
                var memory = manager.AllocateMemory(16);
                Assert.IsNotNull(memory);
                Assert.IsTrue(manager.FreeMemory(memory.Value));
            }
        }

        [TestMethod]
        public void TestFailedAllocation()
        {
            var manager = MemoryManagerFactory.CreateMemoryManager(65536u, 1024, MemoryManagerFactory.AllocationStrategy.WorstFit);
            {
                Assert.IsNull(manager.AllocateMemory(-1));
                Assert.IsNull(manager.AllocateMemory(2048));
            }

            {
                var memory1 = manager.AllocateMemory(256);
                Assert.IsNotNull(memory1);
                Assert.IsTrue(memory1.Value >= 65536 && memory1.Value < 65536 + 1024);
                var memory2 = manager.AllocateMemory(512);
                Assert.IsNotNull(memory2);
                Assert.IsTrue(memory2.Value >= 65536 && memory2.Value < 65536 + 1024);
                Assert.IsTrue(memory2.Value < memory1.Value || memory2.Value >= memory1.Value + 256);
                var memory3 = manager.AllocateMemory(512);
                Assert.IsNull(memory3);
                Assert.IsTrue(manager.FreeMemory(memory2.Value));
                Assert.IsTrue(manager.FreeMemory(memory1.Value));
            }
        }

        [TestMethod]
        public void TestFailedFree()
        {
            var manager = MemoryManagerFactory.CreateMemoryManager(128u, 1024, MemoryManagerFactory.AllocationStrategy.WorstFit);
            {
                Assert.IsFalse(manager.FreeMemory(0u));
                Assert.IsFalse(manager.FreeMemory(1 << 16));

                var memory1 = manager.AllocateMemory(256);
                Assert.IsNotNull(memory1);
                var memory2 = manager.AllocateMemory(512);
                Assert.IsNotNull(memory2);

                Assert.IsFalse(manager.FreeMemory(memory1.Value + 1));
                Assert.IsFalse(manager.FreeMemory(memory2.Value - 1));

                Assert.IsTrue(manager.FreeMemory(memory2.Value));
                Assert.IsTrue(manager.FreeMemory(memory1.Value));
                Assert.IsFalse(manager.FreeMemory(memory1.Value));
                Assert.IsFalse(manager.FreeMemory(memory2.Value));
            }
        }

        [TestMethod]
        public void TestFreeList()
        {
            var manager = MemoryManagerFactory.CreateMemoryManager(128u, 1024, MemoryManagerFactory.AllocationStrategy.WorstFit);
            {
                var memory1 = manager.AllocateMemory(256);
                Assert.IsNotNull(memory1);
                var memory2 = manager.AllocateMemory(128);
                Assert.IsNotNull(memory2);
                var memory3 = manager.AllocateMemory(128);
                Assert.IsNotNull(memory3);
                var memory4 = manager.AllocateMemory(512);
                Assert.IsNotNull(memory4);

                Assert.IsTrue(manager.GetFreeMemories().Count == 0);
            }
        }

        [TestMethod]
        public void TestMemoryLeak()
        {
            var manager = MemoryManagerFactory.CreateMemoryManager(128u, 1024, MemoryManagerFactory.AllocationStrategy.WorstFit);
            {
                var memory1 = manager.AllocateMemory(256);
                Assert.IsNotNull(memory1);
                var memory2 = manager.AllocateMemory(512);
                Assert.IsNotNull(memory2);
                Assert.IsTrue(manager.FreeMemory(memory2.Value));
                Assert.IsTrue(manager.FreeMemory(memory1.Value));

                var freeList = manager.GetFreeMemories();
                Assert.AreEqual(1, freeList.Count);

                var allBlock = freeList.First?.Value;
                Assert.IsNotNull(allBlock);
                if (allBlock is not null)
                {
                    Assert.IsTrue(allBlock.Value.Memory == 128u);
                    Assert.IsTrue(allBlock.Value.Size == 1024);
                }
            }
        }

        [TestMethod]
        public void TestWorstFit()
        {
            var manager = MemoryManagerFactory.CreateMemoryManager(128u, 1024, MemoryManagerFactory.AllocationStrategy.WorstFit);

            {
                // Construct |32| 64 |32| 128 |256| 16 |16| 64 |64| 16 |16| 64 |128| 128

                int[] sizes = { 32, 64, 32, 128, 256, 16, 16, 64, 64, 16, 16, 64, 128, 128 };
                nuint[] memories = new nuint[sizes.Length];
                for (int i = 0; i < sizes.Length; ++i)
                {
                    var tmp = manager.AllocateMemory(sizes[i]);
                    Assert.IsNotNull(tmp);
                    memories[i] = tmp.Value;
                }

                for (int i = 0; i < sizes.Length; i += 2)
                {
                    Assert.IsTrue(manager.FreeMemory(memories[i]));
                }

                {
                    var freeList = manager.GetFreeMemories();
                    Assert.IsTrue(freeList.Count == (sizes.Length + 1) / 2);

                    int i = 0;
                    for (var itr = freeList.First; itr is not null; itr = itr.Next, ++i)
                    {
                        Assert.IsTrue(itr.Value.Size == sizes[i * 2]);
                    }
                }

                var memory1Val = manager.AllocateMemory(96) ?? 0;
                // |32| 64 |32| 128 96 |160| 16 |16| 64 |64| 16 |16| 64 |128| 128
                var memory2Val = manager.AllocateMemory(16) ?? 0;
                // |32| 64 |32| 128 96 16 |144| 16 |16| 64 |64| 16 |16| 64 |128| 128
                var memory3Val = manager.AllocateMemory(256) ?? 0;
                Assert.IsTrue(memory3Val == 0);
                // |32| 64 |32| 128 96 16 |144| 16 |16| 64 |64| 16 |16| 64 |128| 128
                var memory4Val = manager.AllocateMemory(128) ?? 0;
                // |32| 64 |32| 128 96 16 128 |16| 16 |16| 64 |64| 16 |16| 64 |128| 128
                var memory5Val = manager.AllocateMemory(0) ?? 0;
                // |32| 64 |32| 128 96 16 128 |16| 16 |16| 64 |64| 16 |16| 64 |128| 128
                var memory6Val = manager.AllocateMemory(192) ?? 0;
                Assert.IsTrue(memory6Val == 0);
                // |32| 64 |32| 128 96 16 128 |16| 16 |16| 64 |64| 16 |16| 64 |128| 128
                var memory7Val = manager.AllocateMemory(32) ?? 0;
                // |32| 64 |32| 128 96 16 128 |16| 16 |16| 64 |64| 16 |16| 64 32 |96| 128
                manager.FreeMemory(memory4Val);
                // |32| 64 |32| 128 96 16 |144| 16 |16| 64 |64| 16 |16| 64 32 |96| 128
                {
                    var freeList = manager.GetFreeMemories();
                    var freeArr = new int[] { 32, 32, 144, 16, 64, 16, 96 };
                    var q = new Queue<int>(freeArr);
                    Assert.IsTrue(freeList.Count == freeArr.Length);
                    foreach (var memoryBlockInfo in freeList)
                    {
                        Assert.IsTrue(memoryBlockInfo.Size == q.Dequeue());
                    }
                }
            }
        }
    }
}
