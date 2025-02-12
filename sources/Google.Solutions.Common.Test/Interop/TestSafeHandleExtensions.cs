﻿//
// Copyright 2023 Google LLC
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

using Google.Solutions.Common.Interop;
using Google.Solutions.Testing.Apis;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Solutions.Common.Test.Interop
{
    [TestFixture]
    public class TestSafeHandleExtensions
    {
        //---------------------------------------------------------------------
        // ToWaitHandle.
        //---------------------------------------------------------------------

        [Test]
        public void WhenTransferOwnershipIsFalse_ThenToWaitHandleReturnsNonOwningHandle()
        {
            using (var handle = Process.GetCurrentProcess().SafeHandle)
            {
                var waitHandle = handle.ToWaitHandle(false);
                Assert.IsFalse(handle.IsInvalid);

                // Dispose should be a no-op.
                waitHandle.Dispose();
                Assert.IsFalse(handle.IsInvalid);
            }
        }

        [Test]
        public void WhenTransferOwnershipIsTrue_ThenToWaitHandleReturnsOwningHandle()
        {
            var handle = Process.GetCurrentProcess().SafeHandle;
            var waitHandle = handle.ToWaitHandle(false);
            Assert.IsFalse(handle.IsInvalid);

            // Dispose closes the underlying handle.
            waitHandle.Dispose();
            Assert.IsFalse(handle.IsInvalid);
        }

        //---------------------------------------------------------------------
        // WaitAsync.
        //---------------------------------------------------------------------

        [Test]
        public async Task WhenTimeoutElapsed_ThenWaitAsyncReturnsFalse()
        {
            using (var ev = new ManualResetEvent(false))
            {
                var result = await ev
                    .WaitAsync(
                        TimeSpan.FromMilliseconds(10),
                        CancellationToken.None)
                    .ConfigureAwait(false);

                Assert.IsFalse(result);
            }
        }

        [Test]
        public void WhenCancelled_ThenWaitAsyncThrowsException()
        {
            using (var ev = new ManualResetEvent(false))
            using (var cts = new CancellationTokenSource())
            {
                var task = ev.WaitAsync(TimeSpan.FromSeconds(60), cts.Token);
                cts.Cancel();
                
                ExceptionAssert.ThrowsAggregateException<TaskCanceledException>(
                    () => task.Wait());
            }
        }

        [Test]
        public async Task WhenSignalled_ThenWaitAsyncReturnsTrue()
        {
            using (var ev = new ManualResetEvent(true))
            {
                var result = await ev
                    .WaitAsync(
                        TimeSpan.FromMilliseconds(10),
                        CancellationToken.None)
                    .ConfigureAwait(false);

                Assert.IsTrue(result);
            }
        }
    }
}
