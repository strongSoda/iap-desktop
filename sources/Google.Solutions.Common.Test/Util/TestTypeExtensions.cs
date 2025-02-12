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

using Google.Solutions.Common.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Google.Solutions.Common.Test.Util
{
    [TestFixture]
    public class TestTypeExtensions
    {
        //---------------------------------------------------------------------
        // FullName.
        //---------------------------------------------------------------------

        [Test]
        public void WhenTypeNotGeneric_ThenFullNameReturnsName()
        {
            Assert.AreEqual(
                typeof(TestTypeExtensions).Name, 
                typeof(TestTypeExtensions).FullName());
        }

        [Test]
        public void WhenTypeIsGeneric_ThenFullNameReturnsName()
        {
            Assert.AreEqual(
                "KeyValuePair<String,Int32>",
                typeof(KeyValuePair<string, int>).FullName());
        }

        [Test]
        public void WhenTypeIsNestedGeneric_ThenFullNameReturnsName()
        {
            Assert.AreEqual(
                "KeyValuePair<String,Tuple<String,Int32>>",
                typeof(KeyValuePair<string, Tuple<string, int>>).FullName());
        }
    }
}
