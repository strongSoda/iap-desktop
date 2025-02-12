﻿//
// Copyright 2022 Google LLC
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

using Google.Solutions.Common.Diagnostics;
using System;
using System.Reflection;
using System.Text;

namespace Google.Solutions.IapDesktop.Application.Host.Diagnostics
{
    public class BugReport
    {
        private readonly Exception exception;
        private readonly Type source;

        public BugReport()
            : this(null, null)
        {
        }

        public BugReport(
            Type source,
            Exception exception)
        {
            this.source = source;
            this.exception = exception;
        }

        public override string ToString()
        {
            var text = new StringBuilder();

            if (this.exception != null)
            {
                text.Append(this.exception.ToString());

                if (this.exception is ReflectionTypeLoadException tle)
                {
                    text.Append("\nLoader Exceptions:\n");
                    foreach (var e in tle.LoaderExceptions)
                    {
                        text.Append(e.ToString());
                    }
                }

                text.Append("\n\n");
            }

            var cpuArchitecture = Assembly.GetEntryAssembly()?.GetName().ProcessorArchitecture.ToString() ?? "unknown";
            var processBitness = Environment.Is64BitProcess ? 64 : 32;

            text.Append($"Source: {this.source?.Name ?? string.Empty}\n");
            text.Append($"Installed version: {GetType().Assembly.GetName().Version}\n");
            text.Append($"Runtime Version: {ClrVersion.Version}\n");
            text.Append($"OS Version: {Environment.OSVersion}\n");
            text.Append($"Architecture: {cpuArchitecture} ({processBitness} bit)");

            return text.ToString();
        }
    }
}
