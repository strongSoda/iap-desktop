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
using NUnit.Framework;
using System;
using System.Net;

namespace Google.Solutions.Platform.Test.Dispatch
{
    [TestFixture]
    public class TestWin32ProcessFactory
    {
        private static readonly string CmdExe
            = $"{Environment.GetFolderPath(Environment.SpecialFolder.System)}\\cmd.exe";

        private class Win32ProcessFactoryWithEvent : Win32ProcessFactory
        {
            public event EventHandler<IWin32Process> ProcessCreated;

            protected override void OnProcessCreated(IWin32Process process)
            {
                this.ProcessCreated?.Invoke(this, process);
                base.OnProcessCreated(process);
            }
        }

        //---------------------------------------------------------------------
        // CreateProcess.
        //---------------------------------------------------------------------

        [Test]
        public void WhenExecutableNotFound_ThenCreateProcessThrowsException()
        {
            var factory = new Win32ProcessFactory();

            Assert.Throws<DispatchException>(() => factory.CreateProcess("doesnotexist.exe", null));
        }

        [Test]
        public void WhenExecutablePathFound_ThenCreateProcessSucceeds()
        {
            var factory = new Win32ProcessFactory();

            using (var process = factory.CreateProcess(CmdExe, null))
            {
                Assert.IsNotNull(process.Handle);
                Assert.IsFalse(process.Handle.IsInvalid);

                process.Terminate(1);
            }
        }

        [Test]
        public void WhenOnProcessCreatedFails_ThenCreateProcessDisposesProcess()
        {
            var factory = new Win32ProcessFactoryWithEvent();
            IWin32Process createdProcess = null;
            factory.ProcessCreated += (_, p) =>
            {
                createdProcess = p;
                throw new InvalidOperationException("mock");
            };

            Assert.Throws<InvalidOperationException>(
                () => factory.CreateProcess(CmdExe, null));

            Assert.IsTrue(((Win32Process)createdProcess).IsDisposed);
        }

        //---------------------------------------------------------------------
        // CreateProcessAsUser.
        //---------------------------------------------------------------------

        [Test]
        public void WhenUsingDomainCredentialsForNetonlyLogon_ThenCreateProcessAsUserSucceeds()
        {
            var factory = new Win32ProcessFactory();

            using (var process = factory.CreateProcessAsUser(
                CmdExe,
                null,
                LogonFlags.NetCredentialsOnly,
                new NetworkCredential("user", "invalid", "domain")))
            {
                Assert.IsNotNull(process.Handle);
                Assert.IsFalse(process.Handle.IsInvalid);

                process.Terminate(1);
            }
        }

        [Test]
        public void WhenUsingDomainCredentialsInBackslashNotation_ThenCreateProcessAsUserSucceeds()
        {
            var factory = new Win32ProcessFactory();

            using (var process = factory.CreateProcessAsUser(
                CmdExe,
                null,
                LogonFlags.NetCredentialsOnly,
                new NetworkCredential("example\\user", "invalid", null)))
            {
                Assert.IsNotNull(process.Handle);
                Assert.IsFalse(process.Handle.IsInvalid);

                process.Terminate(1);
            }
        }

        [Test]
        public void WhenUsingDomainCredentialsInUpnNotation_ThenCreateProcessAsUserSucceeds()
        {
            var factory = new Win32ProcessFactory();

            using (var process = factory.CreateProcessAsUser(
                CmdExe,
                null,
                LogonFlags.NetCredentialsOnly,
                new NetworkCredential("user@example.com", "invalid", null)))
            {
                Assert.IsNotNull(process.Handle);
                Assert.IsFalse(process.Handle.IsInvalid);

                process.Terminate(1);
            }
        }

        [Test]
        public void WhenUsingInvalidLocalCredentialsForNetonlyLogon_ThenCreateProcessAsUserThrowsException()
        {
            var factory = new Win32ProcessFactory();

            Assert.Throws<DispatchException>(
                () => factory.CreateProcessAsUser(
                CmdExe,
                null,
                LogonFlags.NetCredentialsOnly,
                new NetworkCredential("invalid", "invalid")));
        }


        [Test]
        public void WhenOnProcessCreatedFails_ThenCreateProcessAsUserDisposesProcess()
        {
            var factory = new Win32ProcessFactoryWithEvent();
            IWin32Process createdProcess = null;
            factory.ProcessCreated += (_, p) =>
            {
                createdProcess = p;
                throw new InvalidOperationException("mock");
            };

            Assert.Throws<InvalidOperationException>(
                () => factory.CreateProcessAsUser(
                    CmdExe,
                    null,
                    LogonFlags.NetCredentialsOnly,
                    new NetworkCredential("user", "invalid", "domain")));

            Assert.IsTrue(((Win32Process)createdProcess).IsDisposed);
        }
    }
}
