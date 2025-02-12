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

using Google.Solutions.Apis.Diagnostics;

namespace Google.Solutions.Apis
{
    internal static class HelpTopics
    {
        public static readonly IHelpTopic ProjectAccessControl = new HelpTopic(
            "Project access control",
            "https://cloud.google.com/compute/docs/access");

        public static readonly IHelpTopic LocateInstanceIpAddress = new HelpTopic(
            "Locating IP addresses for an instance",
            "https://cloud.google.com/compute/docs/instances/view-ip-address#console");

        public static readonly IHelpTopic PermissionsToResetWindowsUser = new HelpTopic(
            "Generating Windows passwords",
            "https://cloud.google.com/compute/docs/instances/windows/creating-passwords-for-windows-instances#permissions-required-for-this-task");

        public static readonly IHelpTopic ManagingOsLogin = new HelpTopic(
            "Managing OS Login",
            "https://cloud.google.com/compute/docs/oslogin/manage-oslogin-in-an-org");

        public static readonly IHelpTopic WorkforceIdentityLimitations = new HelpTopic(
            "Workforce identity federation: supported products and limitations",
            "https://cloud.google.com/iam/docs/federated-identity-supported-services");
    }
}
