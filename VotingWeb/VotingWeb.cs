// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace VotingWeb
{
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
    using Microsoft.ServiceFabric.Services.Communication.Runtime;
    using Microsoft.ServiceFabric.Services.Runtime;
    using Microsoft.Extensions.Configuration;
   

    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class VotingWeb : StatelessService
    {
        public VotingWeb(StatelessServiceContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(
                    serviceContext =>
                        new KestrelCommunicationListener(
                            serviceContext,
                            "EndpointHttps",
                            (url, listener) =>
                            {
                                ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");

                                return new WebHostBuilder()
                                    .UseKestrel(opt =>
                                    {
                                        int port = serviceContext.CodePackageActivationContext.GetEndpoint("EndpointHttps").Port;
                                        opt.Listen(IPAddress.IPv6Any, port, listenOptions =>
                                        {
                                            listenOptions.UseHttps(FindMatchingCertificateBySubject("CN=localhost"));
                                            listenOptions.NoDelay = true;
                                        });
                                    })
                                    .ConfigureAppConfiguration((builderContext, config) =>
                                    {
                                        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                                    })
                                    .ConfigureLogging(logging =>
                                    {
                                        logging.ClearProviders();
                                        logging.AddApplicationInsights();
                                    })
                                    .ConfigureServices(
                                        services => services
                                            .AddSingleton<HttpClient>(new HttpClient())
                                            .AddSingleton<FabricClient>(new FabricClient())
                                            .AddSingleton<StatelessServiceContext>(serviceContext))
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                    .UseUrls(url)
                                    .Build();
                            }))
            };
        }

        private X509Certificate2 FindMatchingCertificateBySubject(string subjectCommonName)
        {
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);
                var certCollection = store.Certificates;
                var matchingCerts = new X509Certificate2Collection();

                foreach (var enumeratedCert in certCollection)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(subjectCommonName, enumeratedCert.GetNameInfo(X509NameType.SimpleName, forIssuer: false))
                      && DateTime.Now < enumeratedCert.NotAfter
                      && DateTime.Now >= enumeratedCert.NotBefore)
                    {
                        matchingCerts.Add(enumeratedCert);
                    }
                }

                if (matchingCerts.Count == 0)
                {
                    throw new Exception($"Could not find a match for a certificate with subject 'CN={subjectCommonName}'.");
                }

                return matchingCerts[0];
            }
        }

        /// <summary>
        /// Constructs a service name for a specific poll.
        /// Example: fabric:/VotingApplication/polls/name-of-poll
        /// </summary>
        /// <param name="poll"></param>
        /// <returns></returns>
        internal static Uri GetVotingDataServiceName(ServiceContext context)
        {
            return new Uri($"{context.CodePackageActivationContext.ApplicationName}/VotingData");
        }
    }
}
