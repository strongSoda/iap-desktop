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
using Google.Solutions.IapDesktop.Core.ObjectModel;
using Google.Solutions.Mvvm.Binding;
using System;
using System.Linq;

namespace Google.Solutions.IapDesktop.Extensions.Diagnostics.ToolWindows
{
    [Service]
    public class DebugServiceRegistryViewModel : ViewModelBase
    {
        public DebugServiceRegistryViewModel(IServiceProvider serviceProvider)
        {
            this.RegisteredServices = new RangeObservableCollection<Service>();
            if (serviceProvider is ServiceRegistry registry)
            {
                this.RegisteredServices.AddRange(registry
                    .Registrations
                    .Select(kvp => new Service(kvp.Key, kvp.Value)));
            }
        }

        //---------------------------------------------------------------------
        // Observable properties.
        //---------------------------------------------------------------------

        internal RangeObservableCollection<Service> RegisteredServices { get; }

        //---------------------------------------------------------------------
        // Inner classes.
        //---------------------------------------------------------------------

        public class Service
        {
            public Service(Type serviceType, ServiceLifetime lifetime)
            {
                this.ServiceType = serviceType.ExpectNotNull(nameof(serviceType));
                this.Lifetime = lifetime.ExpectNotNull(nameof(lifetime));
            }

            public Type ServiceType { get; }
            public ServiceLifetime Lifetime { get; }
        }
    }
}
