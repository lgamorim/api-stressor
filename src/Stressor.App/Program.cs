using Microsoft.Extensions.DependencyInjection;
using Stressor.App;
using Stressor.Core;

var services = new ServiceCollection();
services.AddStressorCore();
services.AddHttpClient("stressor");

var runner = new StressorAppRunner(services.BuildServiceProvider());
return await runner.RunAsync(args);
