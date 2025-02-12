﻿//
// Copyright 2020 Google LLC
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
using Google.Solutions.IapDesktop.Application.Profile.Settings;
using Google.Solutions.IapDesktop.Core.ObjectModel;
using Google.Solutions.IapDesktop.Core.ProjectModel;
using Google.Solutions.IapDesktop.Extensions.Session.Protocol;
using System;

namespace Google.Solutions.IapDesktop.Extensions.Session.Settings
{
    public interface IConnectionSettingsService
    {
        bool IsConnectionSettingsAvailable(IProjectModelNode node);
        IPersistentSettingsCollection<ConnectionSettingsBase> GetConnectionSettings(
            IProjectModelNode node);
    }

    [Service(typeof(IConnectionSettingsService))]
    public class ConnectionSettingsService : IConnectionSettingsService
    {
        private readonly ConnectionSettingsRepository repository;

        //---------------------------------------------------------------------
        // Ctor.
        //---------------------------------------------------------------------

        public ConnectionSettingsService(
            ConnectionSettingsRepository settingsRepository)
        {
            this.repository = settingsRepository.ExpectNotNull(nameof(settingsRepository));
        }

        //---------------------------------------------------------------------
        // IConnectionSettingsService.
        //---------------------------------------------------------------------

        public bool IsConnectionSettingsAvailable(IProjectModelNode node)
        {
            return node is IProjectModelProjectNode ||
                   node is IProjectModelZoneNode ||
                   node is IProjectModelInstanceNode;
        }

        public IPersistentSettingsCollection<ConnectionSettingsBase> GetConnectionSettings(
            IProjectModelNode node)
        {
            if (node is IProjectModelProjectNode projectNode)
            {
                return this.repository.GetProjectSettings(projectNode.Project.ProjectId)
                    .ToPersistentSettingsCollection(s => this.repository.SetProjectSettings(s));
            }
            else if (node is IProjectModelZoneNode zoneNode)
            {
                var projectSettings = this.repository.GetProjectSettings(
                    zoneNode.Zone.ProjectId);
                var zoneSettings = this.repository.GetZoneSettings(
                    zoneNode.Zone.ProjectId,
                    zoneNode.Zone.Name);

                // Apply overlay to get effective settings.
                return projectSettings
                    .OverlayBy(zoneSettings)
                    .ToPersistentSettingsCollection(s => this.repository.SetZoneSettings(s));
            }
            else if (node is IProjectModelInstanceNode vmNode)
            {
                var projectSettings = this.repository.GetProjectSettings(
                    vmNode.Instance.ProjectId);
                var zoneSettings = this.repository.GetZoneSettings(
                    vmNode.Instance.ProjectId,
                    vmNode.Instance.Zone);
                var instanceSettings = this.repository.GetVmInstanceSettings(
                    vmNode.Instance.ProjectId,
                    vmNode.Instance.Name);

                // Apply overlay to get effective settings.
                return projectSettings
                    .OverlayBy(zoneSettings)
                    .OverlayBy(instanceSettings)

                    // Save back to same repository.
                    .ToPersistentSettingsCollection(s => this.repository.SetVmInstanceSettings(s))

                    // Hide any settings that are not applicable to this instance.
                    .ToFilteredSettingsCollection((coll, setting) => coll.AppliesTo(setting, vmNode));
            }
            else
            {
                throw new ArgumentException("Unsupported node type: " + node.GetType().Name);
            }
        }
    }
}
