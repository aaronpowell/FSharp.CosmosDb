module Config

open Microsoft.Extensions.Configuration
open System.IO
open System

let environmentName =
    match Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") with
    | ""
    | null -> "Development"
    | _ -> Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")

let private builder =
    JsonConfigurationExtensions
        .AddJsonFile(
            JsonConfigurationExtensions.AddJsonFile(
                FileConfigurationExtensions.SetBasePath(ConfigurationBuilder(), Directory.GetCurrentDirectory()),
                "appsettings.json",
                true,
                true
            ),
            sprintf "appsettings.%s.json" environmentName,
            true,
            true
        )
        .AddEnvironmentVariables()

let config = builder.Build()
