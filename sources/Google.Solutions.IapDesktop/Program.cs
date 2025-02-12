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

using Google.Apis.Util;
using Google.Solutions.Apis;
using Google.Solutions.Apis.Analytics;
using Google.Solutions.Apis.Auth;
using Google.Solutions.Apis.Auth.Gaia;
using Google.Solutions.Apis.Auth.Iam;
using Google.Solutions.Apis.Client;
using Google.Solutions.Apis.Compute;
using Google.Solutions.Apis.Crm;
using Google.Solutions.Apis.Logging;
using Google.Solutions.Common;
using Google.Solutions.Common.Diagnostics;
using Google.Solutions.Common.Util;
using Google.Solutions.Iap;
using Google.Solutions.Iap.Net;
using Google.Solutions.IapDesktop.Application;
using Google.Solutions.IapDesktop.Application.Host;
using Google.Solutions.IapDesktop.Application.Host.Adapters;
using Google.Solutions.IapDesktop.Application.Host.Diagnostics;
using Google.Solutions.IapDesktop.Application.Profile;
using Google.Solutions.IapDesktop.Application.Profile.Auth;
using Google.Solutions.IapDesktop.Application.Profile.Settings;
using Google.Solutions.IapDesktop.Application.Theme;
using Google.Solutions.IapDesktop.Application.ToolWindows.ProjectExplorer;
using Google.Solutions.IapDesktop.Application.Windows;
using Google.Solutions.IapDesktop.Application.Windows.About;
using Google.Solutions.IapDesktop.Application.Windows.Auth;
using Google.Solutions.IapDesktop.Application.Windows.Dialog;
using Google.Solutions.IapDesktop.Application.Windows.Help;
using Google.Solutions.IapDesktop.Application.Windows.Options;
using Google.Solutions.IapDesktop.Application.Windows.ProjectExplorer;
using Google.Solutions.IapDesktop.Application.Windows.ProjectPicker;
using Google.Solutions.IapDesktop.Core;
using Google.Solutions.IapDesktop.Core.ClientModel.Protocol;
using Google.Solutions.IapDesktop.Core.ClientModel.Transport;
using Google.Solutions.IapDesktop.Core.ObjectModel;
using Google.Solutions.IapDesktop.Core.ProjectModel;
using Google.Solutions.IapDesktop.Windows;
using Google.Solutions.Mvvm.Binding;
using Google.Solutions.Mvvm.Interop;
using Google.Solutions.Platform;
using Google.Solutions.Platform.Cryptography;
using Google.Solutions.Platform.Dispatch;
using Google.Solutions.Platform.Net;
using Google.Solutions.Platform.Security;
using Google.Solutions.Ssh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

#pragma warning disable CA1031 // Do not catch general exception types

namespace Google.Solutions.IapDesktop
{
    public class Program : SingletonApplicationBase
    {
        private static readonly Version Windows11 = new Version(10, 0, 22000, 0);
        private static readonly Version WindowsServer2022 = new Version(10, 0, 20348, 0);

        private static bool tracingEnabled = false;

        private static readonly TraceSource[] TraceSources = new[]
        {
            ApiTraceSource.Log,
            PlatformTraceSource.Log,
            CommonTraceSource.Log,
            IapTraceSource.Log,
            SshTraceSource.Log,
            ApplicationTraceSource.Log,
            CoreTraceSource.Log,
        };

        public static string LogFile =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Google",
                "IAP Desktop",
                "Logs",
                $"{DateTime.Now:yyyy-MM-dd_HHmm}.log");

        public static bool IsLoggingEnabled
        {
            get
            {
                return tracingEnabled;
            }
            set
            {
                tracingEnabled = value;


                if (tracingEnabled)
                {
                    var logFilePath = LogFile;
                    Directory.CreateDirectory(new FileInfo(logFilePath).DirectoryName);
                    var logListener = new TextWriterTraceListener(logFilePath);

                    foreach (var trace in TraceSources)
                    {
                        trace.Listeners.Add(new DefaultTraceListener());
                        trace.Listeners.Add(logListener);
                        trace.Switch.Level = SourceLevels.Verbose;
                    }
                }
                else
                {
                    foreach (var trace in TraceSources)
                    {
                        foreach (var listener in trace.Listeners.Cast<TraceListener>())
                        {
                            listener.Flush();
                        }

                        trace.Switch.Level = SourceLevels.Off;
                    }
                }
            }
        }

        private IEnumerable<Assembly> LoadExtensionAssemblies()
        {
            var deprecatedExtensions = new[]
            {
                "google.solutions.iapdesktop.extensions.rdp.dll",
                "google.solutions.iapdesktop.extensions.activity.dll",
                "google.solutions.iapdesktop.extensions.os.dll",
                "google.solutions.iapdesktop.extensions.shell.dll"
            };
            return Directory.GetFiles(
                Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location),
                    "*.Extensions.*.dll")
                .Where(dllPath => !deprecatedExtensions.Contains(new FileInfo(dllPath).Name.ToLower()))
                .Select(dllPath => Assembly.LoadFrom(dllPath));
        }

        private static UserProfile LoadProfileOrExit(Install install, CommandLineOptions options)
        {
            try
            {
                return UserProfile.OpenProfile(install, options.Profile);
            }
            catch (Exception e)
            {
                MessageBox.Show(
                    e.Message,
                    "Profile",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                Environment.Exit(1);
                throw new InvalidOperationException();
            }
        }

        private static IAuthorization AuthorizeOrExit(IServiceProvider serviceProvider)
        {
            var theme = serviceProvider.GetService<IThemeService>().DialogTheme;
            Debug.Assert(theme != null);

            using (var dialog = serviceProvider
                .GetDialog<AuthorizeView, AuthorizeViewModel>(theme))
            {
                //
                // Initialize the view model.
                //
                dialog.ViewModel.DeviceEnrollment = DeviceEnrollment.Create(
                    new CertificateStore(),
                    serviceProvider.GetService<IRepository<IAccessSettings>>());
                dialog.ViewModel.ClientRegistrations = new List<OidcClientRegistration>()
                {
                    new OidcClientRegistration(
                        OidcIssuer.Gaia,
                        OAuthClient.Secrets.ClientId,
                        OAuthClient.Secrets.ClientSecret,
                        "/authorize/"),
                    new OidcClientRegistration(
                        OidcIssuer.Sts,
                        OAuthClient.SdkSecrets.ClientId,
                        OAuthClient.SdkSecrets.ClientSecret,
                        "/")
                };

                //
                // Allow recovery from common errors.
                //
                dialog.ViewModel.OAuthScopeNotGranted += (_, retryArgs) =>
                {
                    //
                    // User did not grant 'cloud-platform' scope.
                    //
                    using (var scopeDialog = serviceProvider
                        .GetDialog<OAuthScopeNotGrantedView, OAuthScopeNotGrantedViewModel>(theme))
                    {
                        retryArgs.Retry = scopeDialog.ShowDialog(dialog.ViewModel.View) == DialogResult.OK;
                    }
                };

                dialog.ViewModel.NetworkError += (_, retryArgs) =>
                {
                    //
                    // This exception might be due to a missing/incorrect proxy
                    // configuration, so give the user a chance to change proxy
                    // settings.
                    //
                    try
                    {
                        if (serviceProvider.GetService<ITaskDialog>()
                            .ShowOptionsTaskDialog(
                                dialog.ViewModel.View,
                                TaskDialogIcons.TD_ERROR_ICON,
                                "Authorization failed",
                                "IAP Desktop failed to complete the OAuth authorization. " +
                                    "This might be due to network communication issues.",
                                retryArgs.Exception.Message,
                                retryArgs.Exception.FullMessage(),
                                new[]
                                {
                            "Change network settings"
                                },
                                null,
                                out var _) == 0)
                        {
                            //
                            // Open settings.
                            //
                            retryArgs.Retry = OptionsDialog.Show(
                                dialog.ViewModel.View,
                                (IServiceCategoryProvider)serviceProvider) == DialogResult.OK;
                        }
                    }
                    catch (OperationCanceledException)
                    { }
                };

                dialog.ViewModel.ShowOptions += (_, args) =>
                {
                    using (var scopeDialog = serviceProvider
                        .GetDialog<AuthorizeOptionsView, AuthorizeOptionsViewModel>(theme))
                    {
                        scopeDialog.ShowDialog(dialog.ViewModel.View);
                    }
                };

                if (dialog.ShowDialog(null) == DialogResult.OK)
                {
                    Debug.Assert(dialog.ViewModel.Authorization != null);

                    return dialog.ViewModel.Authorization;
                }
                else
                {
                    //
                    // User closed the dialog without completing the sign-in.
                    //

                    //
                    // Ensure logs are flushed.
                    //
                    IsLoggingEnabled = false;

                    Environment.Exit(1);
                    throw new InvalidOperationException();
                }
            }
        }

        //---------------------------------------------------------------------
        // SingletonApplicationBase overrides.
        //---------------------------------------------------------------------

        private MainForm initializedMainForm = null;
        private readonly ManualResetEvent mainFormInitialized = new ManualResetEvent(false);

        private readonly CommandLineOptions commandLineOptions;

        internal Program(
            string name,
            CommandLineOptions commandLineOptions)
            : base(name)
        {
            this.commandLineOptions = commandLineOptions;
        }

        protected override int HandleFirstInvocation(string[] args)
        {
            IsLoggingEnabled = this.commandLineOptions.IsLoggingEnabled;

            //
            // Set up process mitigations. This must be done early, otherwise it's
            // ineffective.
            //
            try
            {
                ProcessMitigations.Apply();
            }
            catch (Exception e)
            {
                ApplicationTraceSource.Log.TraceError(e);
            }

#if DEBUG
            ApplicationTraceSource.Log.Switch.Level = SourceLevels.Verbose;
            //SshTraceSources.Default.Switch.Level = SourceLevels.Verbose;
#endif

            if (Environment.OSVersion.Version >= Windows11 ||
                Environment.OSVersion.Version >= WindowsServer2022)
            {
                //
                // Windows 2022 and Windows 11 fully support TLS 1.3:
                // https://docs.microsoft.com/en-us/windows/win32/secauthn/protocols-in-tls-ssl--schannel-ssp-
                //
                System.Net.ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 |
                    SecurityProtocolType.Tls11 |
                    (SecurityProtocolType)0x3000; // TLS 1.3
            }
            else
            {
                //
                // Windows 10 and below don't properly support TLS 1.3 yet.
                //
                System.Net.ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 |
                    SecurityProtocolType.Tls11;
            }

            //
            // Install patches requires for IAP.
            //
            try
            {
                SystemPatch.UnrestrictUserAgentHeader.Install();
            }
            catch (InvalidOperationException e)
            {
                ApplicationTraceSource.Log.TraceWarning(
                    "Installing UnrestrictUserAgentHeader patch failed: {0}", e);
            }

            try
            {
                WebSocket.RegisterPrefixes();
                SystemPatch.SetUsernameAsHostHeaderForWssRequests.Install();
            }
            catch (Exception e)
            {
                ApplicationTraceSource.Log.TraceWarning(
                    "Installing SetUsernameAsHostHeaderForWssRequests patch failed: {0}", e);
            }

            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            //
            // Set up layers. Services in a layer can lookup services in a lower layer,
            // but not in a higher layer.
            //
            var preAuthLayer = new ServiceRegistry();

            var install = new Install(Install.DefaultBaseKeyPath);
            using (var profile = LoadProfileOrExit(install, this.commandLineOptions))
            using (var processFactory = new Win32ChildProcessFactory(true))
            {
                Debug.Assert(!Install.IsExecutingTests);

                // 
                // Load pre-auth layer: Platform abstractions, API adapters.
                //
                // We can only load and access services that don't require
                // authorization. In particular, this means that we cannot access
                // any Google APIs.
                //
                preAuthLayer.AddSingleton<IInstall>(install);
                preAuthLayer.AddSingleton<UserAgent>(Install.UserAgent);
                preAuthLayer.AddSingleton(profile);

                preAuthLayer.AddSingleton<IClock>(SystemClock.Default);
                preAuthLayer.AddTransient<IConfirmationDialog, ConfirmationDialog>();
                preAuthLayer.AddTransient<ITaskDialog, TaskDialog>();
                preAuthLayer.AddTransient<ICredentialDialog, CredentialDialog>();
                preAuthLayer.AddTransient<IInputDialog, InputDialog>();
                preAuthLayer.AddTransient<IExceptionDialog, ExceptionDialog>();
                preAuthLayer.AddTransient<IOperationProgressDialog, OperationProgressDialog>();
                preAuthLayer.AddTransient<INotifyDialog, NotifyDialog>();

                preAuthLayer.AddSingleton<IExternalRestAdapter, ExternalRestAdapter>();
                preAuthLayer.AddTransient<HelpAdapter>();
                preAuthLayer.AddTransient<IGithubAdapter, GithubAdapter>();
                preAuthLayer.AddTransient<BuganizerAdapter>();
                preAuthLayer.AddTransient<IHttpProxyAdapter, HttpProxyAdapter>();

                preAuthLayer.AddSingleton<ProtocolRegistry>();

                preAuthLayer.AddSingleton<IWin32ProcessFactory>(processFactory);
                preAuthLayer.AddSingleton<IWin32ProcessSet>(processFactory);

                //
                // Load settings.
                //
                var appSettingsRepository = new ApplicationSettingsRepository(
                    profile.SettingsKey.CreateSubKey("Application"),
                    profile.MachinePolicyKey?.OpenSubKey("Application"),
                    profile.UserPolicyKey?.OpenSubKey("Application"));
                preAuthLayer.AddSingleton<IRepository<IApplicationSettings>>(appSettingsRepository);

                if (appSettingsRepository.IsPolicyPresent)
                {
                    //
                    // If there are policies in place, mark the UA as
                    // Enterprise-managed.
                    //
                    Install.UserAgent.Extensions = "Enterprise";
                }

                var authSettingsRepository = new AuthSettingsRepository(
                    profile.SettingsKey.CreateSubKey("Auth"));
                preAuthLayer.AddSingleton<IRepository<IAuthSettings>>(authSettingsRepository);
                preAuthLayer.AddSingleton<IOidcOfflineCredentialStore>(authSettingsRepository);

                var accessSettingsRepository = new AccessSettingsRepository(
                    profile.SettingsKey.CreateSubKey("Application"),
                    profile.MachinePolicyKey?.OpenSubKey("Application"),
                    profile.UserPolicyKey?.OpenSubKey("Application"));
                preAuthLayer.AddSingleton<IRepository<IAccessSettings>>(accessSettingsRepository);
                preAuthLayer.AddSingleton<IRepository<IThemeSettings>>(new ThemeSettingsRepository(
                    profile.SettingsKey.CreateSubKey("Theme")));
                preAuthLayer.AddSingleton(new ToolWindowStateRepository(
                    profile.SettingsKey.CreateSubKey("ToolWindows")));

                preAuthLayer.AddSingleton<IBindingContext, ViewBindingContext>();
                preAuthLayer.AddSingleton<IThemeService, ThemeService>();
                preAuthLayer.AddTransient<IQuarantine, Quarantine>();
                preAuthLayer.AddTransient<IBrowserProtocolRegistry, BrowserProtocolRegistry>();

                //
                // Configure networking settings.
                //
                // NB. Until now, no network connections have been made.
                //
                var appSettings = appSettingsRepository.GetSettings();
                try
                {
                    //
                    // Activate proxy settings based on app settings.
                    //
                    preAuthLayer.GetService<IHttpProxyAdapter>().ActivateSettings(appSettings);
                }
                catch (Exception)
                {
                    // Settings invalid -> ignore.
                }

                //
                // Register and configure API client endpoints.
                //
                var serviceRoute = ServiceRoute.Public;
                {
                    var accessSettings = accessSettingsRepository.GetSettings();
                    if (accessSettings.PrivateServiceConnectEndpoint.StringValue is var pscEndpoint &&
                        !string.IsNullOrEmpty(pscEndpoint))
                    {
                        //
                        // Enable PSC.
                        //
                        serviceRoute = new ServiceRoute(pscEndpoint);
                    }

                    //
                    // Set connection pool limit. This limit applies per endpoint.
                    //
                    ServicePointManager.DefaultConnectionLimit
                        = accessSettings.ConnectionLimit.IntValue;
                }

                preAuthLayer.AddSingleton(serviceRoute);
                preAuthLayer.AddSingleton(GaiaOidcClient.CreateEndpoint(serviceRoute));
                preAuthLayer.AddSingleton(WorkforcePoolClient.CreateEndpoint(serviceRoute));
                preAuthLayer.AddSingleton(ResourceManagerClient.CreateEndpoint(serviceRoute));
                preAuthLayer.AddSingleton(ComputeEngineClient.CreateEndpoint(serviceRoute));
                preAuthLayer.AddSingleton(OsLoginClient.CreateEndpoint(serviceRoute));
                preAuthLayer.AddSingleton(LoggingClient.CreateEndpoint(serviceRoute));
                preAuthLayer.AddSingleton(IapClient.CreateEndpoint(serviceRoute));

                //
                // Enable telemetry if the user allows it. Do this before
                // authorization takes place.
                //
                preAuthLayer.AddSingleton<ITelemetryCollector>(new TelemetryCollector(
                    new MeasurementClient(
                        MeasurementClient.CreateEndpoint(),
                        Install.UserAgent,
                        AnalyticsStream.ApiKey,
                        AnalyticsStream.MeasurementId),
                    install)
                {
                    Enabled = appSettings.IsTelemetryEnabled.BoolValue
                });

                preAuthLayer.AddTransient<AuthorizeView>();
                preAuthLayer.AddTransient<AuthorizeViewModel>();
                preAuthLayer.AddTransient<AuthorizeOptionsView>();
                preAuthLayer.AddTransient<AuthorizeOptionsViewModel>();
                preAuthLayer.AddTransient<OAuthScopeNotGrantedView>();
                preAuthLayer.AddTransient<OAuthScopeNotGrantedViewModel>();
                preAuthLayer.AddTransient<PropertiesView>();
                preAuthLayer.AddTransient<PropertiesViewModel>();

                var authorization = AuthorizeOrExit(preAuthLayer);

                //
                // Authorization complete, now the main part of the application
                // can be initialized.
                //
                // Load main layer, containing everything else (except for
                // extensions).
                //
                var mainLayer = new ServiceRegistry(preAuthLayer);
                mainLayer.AddSingleton<IAuthorization>(authorization);
                mainLayer.AddTransient<IToolWindowHost, ToolWindowHost>();

                var mainForm = new MainForm(mainLayer)
                {
                    StartupUrl = this.commandLineOptions.StartupUrl,
                    ShowWhatsNew = this.commandLineOptions.IsPostInstall && install.PreviousVersion != null
                };

                mainLayer.AddSingleton<IJobHost>(mainForm);

                //
                // Load main services.
                //
                var eventService = new EventQueue(mainForm);

                //
                // Register API clients as singletons to ensure connection reuse.
                //
                mainLayer.AddSingleton<IResourceManagerClient, ResourceManagerClient>();
                mainLayer.AddSingleton<IComputeEngineClient, ComputeEngineClient>();
                mainLayer.AddSingleton<ILoggingClient, LoggingClient>();
                mainLayer.AddSingleton<IOsLoginClient, OsLoginClient>();
                mainLayer.AddSingleton<IIapClient, IapClient>();
                mainLayer.AddTransient<IAddressResolver, AddressResolver>();

                mainLayer.AddTransient<IAddressResolver, AddressResolver>();
                mainLayer.AddTransient<IWindowsCredentialGenerator, WindowsCredentialGenerator>();
                mainLayer.AddSingleton<IJobService, JobService>();
                mainLayer.AddSingleton<IEventQueue>(eventService);
                mainLayer.AddSingleton<IGlobalSessionBroker, GlobalSessionBroker>();

                var projectRepository = new ProjectRepository(profile.SettingsKey.CreateSubKey("Inventory"));
                mainLayer.AddSingleton<IProjectRepository>(projectRepository);
                mainLayer.AddSingleton<IProjectSettingsRepository>(projectRepository);
                mainLayer.AddSingleton<IProjectWorkspace, ProjectWorkspace>();
                mainLayer.AddTransient<ICloudConsole, CloudConsole>();
                mainLayer.AddTransient<IUpdateCheck, UpdateCheck>();
                mainLayer.AddSingleton<IIapTransportFactory, IapTransportFactory>();
                mainLayer.AddSingleton<IDirectTransportFactory, DirectTransportFactory>();

                //
                // Load windows.
                //
                mainLayer.AddSingleton<IMainWindow>(mainForm);
                mainLayer.AddSingleton<IWin32Window>(mainForm);
                mainLayer.AddTransient<AboutView>();
                mainLayer.AddTransient<AboutViewModel>();
                mainLayer.AddTransient<AccessInfoFlyoutView>();
                mainLayer.AddTransient<AccessInfoViewModel>();
                mainLayer.AddTransient<NewProfileView>();
                mainLayer.AddTransient<NewProfileViewModel>();

                mainLayer.AddTransient<IProjectPickerDialog, ProjectPickerDialog>();
                mainLayer.AddTransient<ProjectPickerView>();
                mainLayer.AddTransient<ProjectPickerViewModel>();

                mainLayer.AddSingleton<IProjectExplorer, ProjectExplorer>();
                mainLayer.AddSingleton<ProjectExplorerView>();
                mainLayer.AddTransient<ProjectExplorerViewModel>();
                mainLayer.AddTransient<ReleaseNotesView>();
                mainLayer.AddTransient<ReleaseNotesViewModel>();
                mainLayer.AddSingleton<UrlCommands>();

                //
                // Load extensions.
                //
                foreach (var extension in LoadExtensionAssemblies())
                {
                    mainLayer.AddExtensionAssembly(extension);
                }

                //
                // Run app.
                //
                this.initializedMainForm = mainForm;
                this.initializedMainForm.Shown += (_, __) =>
                {
                    //
                    // Form is now ready to handle subsequent invocations.
                    //
                    this.mainFormInitialized.Set();
                };
                this.initializedMainForm.FormClosing += (_, __) =>
                {
                    //
                    // Stop handling subsequent invocations.
                    //
                    this.initializedMainForm = null;
                };

                MessageThrottle.Install();

                //
                // Replace the standard WinForms exception dialog.
                //
                System.Windows.Forms.Application.ThreadException += (_, exArgs)
                    => ShowFatalError(exArgs.Exception);

                mainForm.Shown += (_, __) =>
                {
                    //
                    // Try to force the window into the foreground. This might
                    // not be allowed in all circumstances, but ensures that the
                    // window becomes visible after the user has completed a
                    // (browser-based) authorization.
                    //
                    TrySetForegroundWindow(Process.GetCurrentProcess().Id);
                };

                //
                // Show the main window.
                //
                System.Windows.Forms.Application.Run(mainForm);

                if (processFactory.ChildProcesses > 0)
                {
                    //
                    // Instead of killing child processes outright, give
                    // them a chance to close gracefully (and possibly
                    // save any work).
                    //
                    try
                    {
                        WaitDialog.Wait(
                            null,
                            "Waiting for applications to close...",
                            cancellationToken =>
                            {
                                //
                                // Give child processes a fixed time to close,
                                // but the user might cancel early.
                                //
                                return processFactory.CloseAsync(
                                    TimeSpan.FromSeconds(30),
                                    cancellationToken);
                            });
                    }
                    catch (Exception e)
                    {
#if DEBUG
                        if (!e.IsCancellation())
                        {
                            ShowFatalError(e);
                        }
#endif
                    }
                }

                //
                // Ensure logs are flushed.
                //
                IsLoggingEnabled = false;

                return 0;
            }
        }

        protected override int HandleSubsequentInvocation(string[] args)
        {
            var options = CommandLineOptions.ParseOrExit(args);

            //
            // Make sure the main form is ready.
            //
            this.mainFormInitialized.WaitOne();
            if (this.initializedMainForm != null)
            {
                //
                // This method is called on the named pipe server thread - switch to 
                // main thread before doing any GUI stuff.
                //
                this.initializedMainForm.Invoke(((Action)(() =>
                {
                    if (options.StartupUrl != null)
                    {
                        this.initializedMainForm.ConnectToUrl(options.StartupUrl);
                    }
                })));
            }

            return 1;
        }

        protected override void HandleSubsequentInvocationException(Exception e)
            => ShowFatalError(e);

        private static void ShowFatalError(Exception e)
        {
            //
            // Ensure logs are flushed.
            //
            IsLoggingEnabled = false;

            //
            // NB. This could be called on any thread, at any time, so avoid
            // touching the main form.
            //
            ErrorDialog.Show(e);
            Environment.Exit(e.HResult);
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            //
            // Parse command line to catch errors before even passing an invalid
            // command line to another instance of the app.
            //
            var options = CommandLineOptions.ParseOrExit(args);

            try
            {
                var appName = "IapDesktop";
                if (options.Profile != null)
                {
                    //
                    // Incorporate the profile name (if provided) into the
                    // name of the singleton app so that instances can 
                    // coexist if thex use different profiles.
                    //
                    appName += $"_{options.Profile}";
                }

                new Program(appName, options).Run(args);
            }
            catch (Exception e)
            {
                ShowFatalError(e);
            }
        }

        internal static void LaunchNewInstance(CommandLineOptions options)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo()
                {
                    FileName = Assembly.GetExecutingAssembly().Location,
                    Arguments = options.ToString(),
                    WindowStyle = ProcessWindowStyle.Normal
                };
                process.Start();
            }
        }

        /// <summary>
        /// Debug only: Throttles window messages by introducing a sleep
        /// between each message.
        /// 
        /// Can be enabled and disabled by pressing F12.
        /// </summary>
        private class MessageThrottle : IMessageFilter
        {
            public bool IsEnabled { get; private set; } = false;

            public bool PreFilterMessage(ref Message m)
            {
                if ((WindowMessage)m.Msg == WindowMessage.WM_KEYDOWN &&
                    (Keys)m.WParam.ToInt32() == Keys.F12)
                {
                    this.IsEnabled = !this.IsEnabled;
                }

                if (this.IsEnabled)
                {
                    Thread.Sleep(100);
                }

                return false;
            }

            [Conditional("DEBUG")]
            public static void Install()
            {
                System.Windows.Forms.Application.AddMessageFilter(new MessageThrottle());
            }
        }
    }
}