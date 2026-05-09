// <copyright file="AppHost.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.GardenPlotWeb>("gardenplotweb");

builder.Build().Run();
