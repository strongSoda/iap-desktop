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

using Google.Solutions.Platform.Dispatch;
using Google.Solutions.Testing.Apis;
using NUnit.Framework;
using System.ComponentModel;
using System.Diagnostics;

namespace Google.Solutions.Platform.Test.Dispatch
{
    [TestFixture]
    public class TestWtsSession : EquatableFixtureBase<WtsSession, IWtsSession>
    {
        protected override WtsSession CreateInstance()
        {
            return WtsSession.GetCurrent();
        }

        //---------------------------------------------------------------------
        // GetCurrent.
        //---------------------------------------------------------------------

        [Test]
        public void GetCurrent()
        {
            var session = WtsSession.GetCurrent();
            Assert.IsNotNull(session);
        }

        //---------------------------------------------------------------------
        // FromProcessId.
        //---------------------------------------------------------------------

        [Test]
        public void WhenPidIsFromCurrentProcess_ThenFromProcessIdReturnsSession()
        {
            var session = WtsSession.FromProcessId((uint)Process.GetCurrentProcess().Id);
            Assert.IsNotNull(session);
            Assert.AreEqual(WtsSession.GetCurrent(), session);
        }

        [Test]
        public void WhenPidNotFound_ThenFromProcessIdThrowsException()
        {
            Assert.Throws<DispatchException>(
                () => WtsSession.FromProcessId(uint.MaxValue));
        }
    }
}
