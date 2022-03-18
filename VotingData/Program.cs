// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace VotingData
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Fabric;
    using System.Fabric.Description;
    using System.ServiceModel.Channels;
    using System.Threading;
    using Microsoft.ApplicationInsights;
    using Microsoft.ServiceFabric.Services.Runtime;

    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main()
        {
            try
            {
                // The ServiceManifest.XML file defines one or more service type names.
                // Registering a service maps a service type name to a .NET type.
                // When Service Fabric creates an instance of this service type,
                // an instance of the class is created in this host process.

                ServiceRuntime.RegisterServiceAsync("VotingDataType", context => new VotingData(context)).GetAwaiter().GetResult();

                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(VotingData).Name);

                //ServiceEventSource.Current.ServiceMessage(serviceContext, "VotingData Prior to ServiceRuntime.RegisterServiceAsync");
         
                UpdateMetrics();

                // Prevents this host process from terminating so services keeps running. 
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }

            
        }

        private static void UpdateMetrics()
        {
            FabricClient fabricClient = new FabricClient();

            //StatefulServiceUpdateDescription serviceDescription = new StatefulServiceUpdateDescription();
            
            StatefulServiceLoadMetricDescription consequentialMetric = new StatefulServiceLoadMetricDescription();
            consequentialMetric.Name = "ConsequentialMetric";
            consequentialMetric.Weight = ServiceLoadMetricWeight.High;

            StatefulServiceLoadMetricDescription primaryCountMetric = new StatefulServiceLoadMetricDescription();
            primaryCountMetric.Name = "PrimaryCount";
            primaryCountMetric.PrimaryDefaultLoad = 1;
            primaryCountMetric.SecondaryDefaultLoad = 0;
            primaryCountMetric.Weight = ServiceLoadMetricWeight.Medium;

            StatefulServiceLoadMetricDescription replicaCountMetric = new StatefulServiceLoadMetricDescription();
            replicaCountMetric.Name = "ReplicaCount";
            replicaCountMetric.PrimaryDefaultLoad = 1;
            replicaCountMetric.SecondaryDefaultLoad = 1;
            replicaCountMetric.Weight = ServiceLoadMetricWeight.Low;

            StatefulServiceLoadMetricDescription totalCountMetric = new StatefulServiceLoadMetricDescription();
            totalCountMetric.Name = "Count";
            totalCountMetric.PrimaryDefaultLoad = 1;
            totalCountMetric.SecondaryDefaultLoad = 1;
            totalCountMetric.Weight = ServiceLoadMetricWeight.Low;

            int pLoad = 1, sLoad = 1;

            ServiceUpdateDescription serviceUpdateDescription = new StatefulServiceUpdateDescription();

            while (true)
            {
                consequentialMetric.PrimaryDefaultLoad = pLoad++;
                consequentialMetric.SecondaryDefaultLoad = sLoad++;

                serviceUpdateDescription.Metrics = new CustomMetricDescription();

                serviceUpdateDescription.Metrics.Add(consequentialMetric);
                serviceUpdateDescription.Metrics.Add(primaryCountMetric);
                serviceUpdateDescription.Metrics.Add(replicaCountMetric);
                serviceUpdateDescription.Metrics.Add(totalCountMetric);

                fabricClient.ServiceManager.UpdateServiceAsync(new Uri("fabric:/Voting/VotingData"), serviceUpdateDescription).Wait();
                Thread.Sleep(10000); // sleep for 10 second
            }

            
        }
    }

    internal class CustomMetricDescription : KeyedCollection<string, ServiceLoadMetricDescription>
    {
        protected override string GetKeyForItem(ServiceLoadMetricDescription item)
        {
            return item.Name;
        }
    }

}